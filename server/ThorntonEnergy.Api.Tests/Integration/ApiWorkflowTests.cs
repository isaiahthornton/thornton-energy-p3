using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using ThorntonEnergy.Api.Models;
using ThorntonEnergy.Api.Services;
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

    // ---------- signing in (the account status comes from the Customers service) ----------

    private FakeCustomerAccounts Accounts => (FakeCustomerAccounts)factory.Services.GetRequiredService<ICustomerAccounts>();

    [Fact]
    public async Task SuspendedCustomer_CannotLogIn_UntilTheyAreRestored()
    {
        // Arrange: an admin suspends John in the Customers service
        var client = factory.CreateClient();
        Accounts.SetStatus(2, AccountStatus.Suspended);

        // Act
        var whileSuspended = await client.PostAsJsonAsync("/api/auth/login", new { email = "john@test.com", password = TestData.Password });
        Accounts.SetStatus(2, AccountStatus.Active);
        var afterRestore = await client.PostAsJsonAsync("/api/auth/login", new { email = "john@test.com", password = TestData.Password });

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, whileSuspended.StatusCode);
        Assert.Equal(HttpStatusCode.OK, afterRestore.StatusCode);
    }

    [Fact]
    public async Task WhenCustomersIsDown_CustomersGet503_ButAdminsCanStillSignIn()
    {
        // Arrange
        var client = factory.CreateClient();
        Accounts.IsDown = true;

        try
        {
            // Act: a customer login has to check the account; an admin login does not
            var customer = await client.PostAsJsonAsync("/api/auth/login", new { email = "jane@test.com", password = TestData.Password });
            var admin = await client.PostAsJsonAsync("/api/auth/login", new { email = "admin@test.com", password = TestData.Password });

            // Assert
            Assert.Equal(HttpStatusCode.ServiceUnavailable, customer.StatusCode);
            Assert.Equal(HttpStatusCode.OK, admin.StatusCode);
        }
        finally
        {
            Accounts.IsDown = false;
        }
    }

    // ---------- registering ----------

    [Fact]
    public async Task Register_CreatesALogin_ThenRefusesADuplicate_AndUnknownCustomers()
    {
        // Arrange: customers 1 and 2 already have logins; customer 3 (in the fake Customers service) does not
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
