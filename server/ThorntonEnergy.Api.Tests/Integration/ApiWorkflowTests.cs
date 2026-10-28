using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using ThorntonEnergy.Api.Data;
using ThorntonEnergy.Api.Models;
using ThorntonEnergy.Api.Tests.Helpers;

namespace ThorntonEnergy.Api.Tests.Integration;

// Whole workflows through HTTP: create, change, read back. Each test class gets its own in-memory app,
// so these tests cannot disturb the ones in ApiTests (and have their own login rate limit budget).
public class ApiWorkflowTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private static readonly Dictionary<string, string> Tokens = [];

    private async Task<HttpClient> SignedInAsync(string email)
    {
        var client = factory.CreateClient();

        string? token;
        lock (Tokens) Tokens.TryGetValue(email, out token);
        if (token is null)
        {
            var response = await client.PostAsJsonAsync("/api/auth/login", new { email, password = TestData.Password });
            response.EnsureSuccessStatusCode();
            token = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("token").GetString()!;
            lock (Tokens) Tokens[email] = token;
        }

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    // ---------- customers ----------

    [Fact]
    public async Task Customer_CanChangeOwnRatePlan_ButNotStatus_AndNotSomeoneElsesRecord()
    {
        // Arrange
        var jane = await SignedInAsync("jane@test.com");

        // Act
        var ownPlan = await jane.PatchAsJsonAsync("/api/customers/1", new { ratePlanId = 1 });
        var ownStatus = await jane.PatchAsJsonAsync("/api/customers/1", new { status = "closed" });
        var other = await jane.PatchAsJsonAsync("/api/customers/2", new { ratePlanId = 1 });
        var unknownPlan = await jane.PatchAsJsonAsync("/api/customers/1", new { ratePlanId = 99 });

        // Assert
        Assert.Equal(HttpStatusCode.OK, ownPlan.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, ownStatus.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, other.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, unknownPlan.StatusCode);
    }

    [Fact]
    public async Task Admin_CanSuspendACustomer_AndTheyCanNoLongerLogIn()
    {
        // Arrange: a throw-away customer with its own login, so no other test is affected
        var admin = await SignedInAsync("admin@test.com");
        var client = factory.CreateClient();

        // Act
        var suspend = await admin.PatchAsJsonAsync("/api/customers/2", new { status = "suspended" });
        var login = await client.PostAsJsonAsync("/api/auth/login", new { email = "john@test.com", password = TestData.Password });
        var restore = await admin.PatchAsJsonAsync("/api/customers/2", new { status = "active" });

        // Assert
        Assert.Equal(HttpStatusCode.OK, suspend.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, login.StatusCode);
        Assert.Equal(HttpStatusCode.OK, restore.StatusCode);
    }

    // ---------- rate plans ----------

    [Fact]
    public async Task RatePlans_AdminCanCreateUpdateAndDelete_ButNotWhileCustomersAreOnThePlan()
    {
        // Arrange
        var admin = await SignedInAsync("admin@test.com");
        var jane = await SignedInAsync("jane@test.com");
        var plan = new { name = "Night Owl", pricePerKwh = 0.08, monthlyFee = 5, description = "Cheap at night" };

        // Act
        var created = await admin.PostAsJsonAsync("/api/ratePlans", plan);
        var id = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetInt32();
        var replaced = await admin.PutAsJsonAsync($"/api/ratePlans/{id}", new { name = "Night Owl 2", pricePerKwh = 0.07, monthlyFee = 5, description = "Cheaper" });
        var deletedInUse = await admin.DeleteAsync("/api/ratePlans/1"); // customers are on plan 1
        var deleted = await admin.DeleteAsync($"/api/ratePlans/{id}");
        var gone = await admin.GetAsync($"/api/ratePlans/{id}");
        var byCustomer = await jane.PostAsJsonAsync("/api/ratePlans", plan);

        // Assert
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        Assert.Equal(HttpStatusCode.OK, replaced.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, deletedInUse.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, gone.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, byCustomer.StatusCode);
    }

    [Fact]
    public async Task RatePlans_InvalidPriceIsRejected()
    {
        // Arrange
        var admin = await SignedInAsync("admin@test.com");

        // Act
        var response = await admin.PostAsJsonAsync("/api/ratePlans", new { name = "Bad", pricePerKwh = -1, monthlyFee = 5, description = "x" });

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // ---------- registering ----------

    [Fact]
    public async Task Register_CreatesALogin_ThenRefusesADuplicate_AndUnknownCustomers()
    {
        // Arrange: customers 1 and 2 already have logins, so add a new customer without one (straight into the test database)
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Customers.Add(new Customer
            {
                Id = 3, AccountNumber = "TE-100003", FirstName = "New", LastName = "Person", Phone = "3", Email = "new@test.com",
                ServiceAddress = "3 Main St", RatePlanId = 1, Status = AccountStatus.Active,
            });
            db.SaveChanges();
        }
        var anonymous = factory.CreateClient();
        var good = new { accountNumber = "TE-100003", email = "new@test.com", password = "abcdef" };

        // Act
        var first = await anonymous.PostAsJsonAsync("/api/auth/register", good);
        var second = await anonymous.PostAsJsonAsync("/api/auth/register", good);
        var unknown = await anonymous.PostAsJsonAsync("/api/auth/register",
            new { accountNumber = "TE-999999", email = "nobody@test.com", password = "abcdef" });

        // Assert
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, unknown.StatusCode);
    }
}
