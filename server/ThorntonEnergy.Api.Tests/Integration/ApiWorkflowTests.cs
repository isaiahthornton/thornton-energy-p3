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

    // ---------- bills ----------

    [Fact]
    public async Task Admin_CreatesABill_ThePriceComesFromTheRatePlan_AndCustomerCanPayItOnce()
    {
        // Arrange
        var admin = await SignedInAsync("admin@test.com");
        var jane = await SignedInAsync("jane@test.com");

        // Act: the browser sends a wrong total on purpose; the server must ignore it
        var created = await admin.PostAsJsonAsync("/api/bills",
            new { customerId = 1, periodStart = "2030-01-01", periodEnd = "2030-01-31", kwhUsed = 100, amountDue = 1, status = "paid" });
        var bill = await created.Content.ReadFromJsonAsync<JsonElement>();
        var id = bill.GetProperty("id").GetInt32();

        var firstPay = await jane.PatchAsJsonAsync($"/api/bills/{id}", new { status = "paid" });
        var secondPay = await jane.PatchAsJsonAsync($"/api/bills/{id}", new { status = "paid" });

        // Assert: plan is 0.10/kWh + 10.00 fee, so 100 kWh = 20.00
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        Assert.Equal(20.00m, bill.GetProperty("amountDue").GetDecimal());
        Assert.Equal("unpaid", bill.GetProperty("status").GetString());
        Assert.Equal(HttpStatusCode.OK, firstPay.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, secondPay.StatusCode);
    }

    [Fact]
    public async Task Customer_CannotCreateABill()
    {
        // Arrange
        var jane = await SignedInAsync("jane@test.com");

        // Act
        var response = await jane.PostAsJsonAsync("/api/bills",
            new { customerId = 1, periodStart = "2030-03-01", periodEnd = "2030-03-31", kwhUsed = 1 });

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Admin_CreatingADuplicateBill_Returns409()
    {
        // Arrange: customer 2 already has a bill starting 2026-01-01
        var admin = await SignedInAsync("admin@test.com");

        // Act
        var response = await admin.PostAsJsonAsync("/api/bills",
            new { customerId = 2, periodStart = "2026-01-01", periodEnd = "2026-01-31", kwhUsed = 5 });

        // Assert
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task OldUnpaidBill_ComesBackAsOverdue()
    {
        // Arrange: Jane's bill 1 was due in early 2026 and is still unpaid
        var jane = await SignedInAsync("jane@test.com");

        // Act
        var bill = await jane.GetFromJsonAsync<JsonElement>("/api/bills/1");

        // Assert
        Assert.Equal("overdue", bill.GetProperty("status").GetString());
    }

    // ---------- meters ----------

    [Fact]
    public async Task Meters_CustomerSeesOnlyTheirOwn_AdminSeesAll()
    {
        // Arrange
        var jane = await SignedInAsync("jane@test.com");
        var admin = await SignedInAsync("admin@test.com");

        // Act
        var janeMeters = await jane.GetFromJsonAsync<JsonElement>("/api/meters");
        var adminMeters = await admin.GetFromJsonAsync<JsonElement>("/api/meters");
        var forbidden = await jane.GetAsync("/api/meters?customerId=2");

        // Assert
        Assert.Equal(1, janeMeters.GetArrayLength());
        Assert.Equal(1, adminMeters.GetArrayLength());
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
    }

    [Fact]
    public async Task MeterReadings_LowerValueIsRejected_HigherIsAccepted_AndOnlyAdminsMayAdd()
    {
        // Arrange: meter 1 already reads 1000 on 2026-01-01
        var admin = await SignedInAsync("admin@test.com");
        var jane = await SignedInAsync("jane@test.com");

        // Act
        var lower = await admin.PostAsJsonAsync("/api/meterReadings", new { meterId = 1, readingDate = "2026-02-01", readingValue = 5 });
        var higher = await admin.PostAsJsonAsync("/api/meterReadings", new { meterId = 1, readingDate = "2026-02-01", readingValue = 1500 });
        var byCustomer = await jane.PostAsJsonAsync("/api/meterReadings", new { meterId = 1, readingDate = "2026-03-01", readingValue = 2000 });
        var list = await jane.GetFromJsonAsync<JsonElement>("/api/meterReadings?meterId=1");

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, lower.StatusCode);
        Assert.Equal(HttpStatusCode.Created, higher.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, byCustomer.StatusCode);
        Assert.Equal(2, list.GetArrayLength());
    }

    [Fact]
    public async Task MeterStatus_CanBeChangedByAnAdmin()
    {
        // Arrange
        var admin = await SignedInAsync("admin@test.com");

        // Act
        var response = await admin.PatchAsJsonAsync("/api/meters/1", new { status = "inactive" });

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("inactive", (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("status").GetString());
    }

    // ---------- outages ----------

    [Fact]
    public async Task Admin_ResolvingAnOutage_StampsTheResolveTimeOnTheServer()
    {
        // Arrange
        var admin = await SignedInAsync("admin@test.com");
        var created = await admin.PostAsJsonAsync("/api/outages", new { customerId = 2, address = "2 Main St", description = "Dark" });
        var id = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetInt32();

        // Act
        var resolved = await admin.PatchAsJsonAsync($"/api/outages/{id}", new { status = "resolved" });
        var reopened = await admin.PatchAsJsonAsync($"/api/outages/{id}", new { status = "in_progress" });

        // Assert
        var resolvedBody = await resolved.Content.ReadFromJsonAsync<JsonElement>();
        var reopenedBody = await reopened.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(JsonValueKind.String, resolvedBody.GetProperty("timeResolved").ValueKind);
        Assert.Equal(JsonValueKind.Null, reopenedBody.GetProperty("timeResolved").ValueKind);
    }

    [Fact]
    public async Task Admin_ReportingAnOutageWithoutACustomer_Returns400()
    {
        // Arrange
        var admin = await SignedInAsync("admin@test.com");

        // Act
        var response = await admin.PostAsJsonAsync("/api/outages", new { address = "Somewhere", description = "Dark" });

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Customer_CannotUpdateOutageStatus_OrReadAnotherCustomersOutage()
    {
        // Arrange
        var admin = await SignedInAsync("admin@test.com");
        var jane = await SignedInAsync("jane@test.com");
        var created = await admin.PostAsJsonAsync("/api/outages", new { customerId = 2, address = "2 Main St", description = "Dark" });
        var id = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetInt32();

        // Act
        var patch = await jane.PatchAsJsonAsync($"/api/outages/{id}", new { status = "resolved" });
        var read = await jane.GetAsync($"/api/outages/{id}");

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, patch.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, read.StatusCode);
    }

    [Fact]
    public async Task OutageMap_IsAdminOnly()
    {
        // Arrange
        var admin = await SignedInAsync("admin@test.com");
        var jane = await SignedInAsync("jane@test.com");

        // Act
        var forAdmin = await admin.GetAsync("/api/outages/map");
        var forCustomer = await jane.GetAsync("/api/outages/map");

        // Assert: test customers have no coordinates, so the map is empty but allowed
        Assert.Equal(HttpStatusCode.OK, forAdmin.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, forCustomer.StatusCode);
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
