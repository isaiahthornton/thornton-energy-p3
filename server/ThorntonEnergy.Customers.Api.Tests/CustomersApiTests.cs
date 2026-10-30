using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ThorntonEnergy.Shared.Auth;

namespace ThorntonEnergy.Customers.Api.Tests;

// Real HTTP requests against the real Customers service (in memory): who may see what.
// Jane is customer 1, John is customer 2, Sam (3) is suspended. This class never changes data.
public class CustomersApiTests(CustomersApiFactory factory) : IClassFixture<CustomersApiFactory>
{
    private HttpClient Jane() => factory.SignedInAs(UserRole.Customer, customerId: 1);
    private HttpClient Admin() => factory.SignedInAs(UserRole.Admin);
    private HttpClient Anonymous() => factory.CreateClient();

    // ---------- customers ----------

    [Fact]
    public async Task CustomerList_IsAdminOnly()
    {
        // Act
        var anonymous = await Anonymous().GetAsync("/api/customers");
        var asCustomer = await Jane().GetAsync("/api/customers");
        var asAdmin = await Admin().GetAsync("/api/customers");

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, asCustomer.StatusCode);
        Assert.Equal(HttpStatusCode.OK, asAdmin.StatusCode);
        Assert.Equal(3, (await asAdmin.Content.ReadFromJsonAsync<JsonElement>()).GetArrayLength());
    }

    [Fact]
    public async Task Customer_CanReadOnlyTheirOwnRecord()
    {
        // Act
        var own = await Jane().GetAsync("/api/customers/1");
        var other = await Jane().GetAsync("/api/customers/2");

        // Assert
        Assert.Equal(HttpStatusCode.OK, own.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, other.StatusCode);
    }

    // ---------- rate plans ----------

    [Fact]
    public async Task RatePlans_ArePublic()
    {
        // Act: no token at all
        var response = await Anonymous().GetAsync("/api/ratePlans");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(1, (await response.Content.ReadFromJsonAsync<JsonElement>()).GetArrayLength());
    }

    // ---------- internal endpoints (the monolith's login and register) ----------

    [Fact]
    public async Task Internal_WithAServiceToken_ReturnsTheAccountStatus()
    {
        // Act
        var jane = await factory.SignedInAsService().GetFromJsonAsync<JsonElement>("/api/internal/customers/1");
        var sam = await factory.SignedInAsService().GetFromJsonAsync<JsonElement>("/api/internal/customers/3");
        var nobody = await factory.SignedInAsService().GetAsync("/api/internal/customers/999");

        // Assert
        Assert.Equal("active", jane.GetProperty("status").GetString());
        Assert.Equal("suspended", sam.GetProperty("status").GetString());
        Assert.Equal(HttpStatusCode.NotFound, nobody.StatusCode);
    }

    [Fact]
    public async Task Internal_Lookup_NeedsTheAccountNumberAndEmailOfTheSameCustomer()
    {
        // Act
        var match = await factory.SignedInAsService().GetFromJsonAsync<JsonElement>(
            "/api/internal/customers/lookup?accountNumber=TE-100001&email=jane@test.com");
        var mismatch = await factory.SignedInAsService().GetAsync(
            "/api/internal/customers/lookup?accountNumber=TE-100001&email=john@test.com");

        // Assert
        Assert.Equal(1, match.GetProperty("id").GetInt32());
        Assert.Equal(HttpStatusCode.NotFound, mismatch.StatusCode);
    }

    [Fact]
    public async Task Internal_RefusesPeople_EvenAdmins()
    {
        // Act
        var anonymous = await Anonymous().GetAsync("/api/internal/customers/1");
        var asCustomer = await Jane().GetAsync("/api/internal/customers/1");
        var asAdmin = await Admin().GetAsync("/api/internal/customers/1");

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, asCustomer.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, asAdmin.StatusCode);
    }

    [Fact]
    public async Task ServiceToken_CannotUseAdminEndpoints()
    {
        // Act: a service token is not an admin token
        var list = await factory.SignedInAsService().GetAsync("/api/customers");

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, list.StatusCode);
    }

    // ---------- admin reports (used by the monolith's /api/admin dashboard) ----------

    // The reports read a SQL Server view, which SQLite does not have.
    // Their access rules are checked before any database work, so we can still test who gets in.
    [Theory]
    [InlineData("/api/customers/summary")]
    [InlineData("/api/customers/activity")]
    public async Task Reports_RefuseCustomersAndAnonymousCallers(string url)
    {
        // Act
        var asCustomer = await Jane().GetAsync(url);
        var anonymous = await Anonymous().GetAsync(url);

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, asCustomer.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
    }

    [Fact]
    public async Task HealthChecks_ArePublic()
    {
        // Act
        var live = await Anonymous().GetAsync("/health/live");
        var ready = await Anonymous().GetAsync("/health/ready");

        // Assert
        Assert.Equal(HttpStatusCode.OK, live.StatusCode);
        Assert.Equal(HttpStatusCode.OK, ready.StatusCode);
    }
}
