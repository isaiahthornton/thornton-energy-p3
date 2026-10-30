using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using ThorntonEnergy.Shared.Auth;

namespace ThorntonEnergy.Billing.Api.Tests;

// Real HTTP requests against the real Billing service (in memory): the security rules as a client sees them.
// Ported from the monolith's ApiTests bill tests. Jane is customer 1 (bill 1), John is customer 2 (bill 2).
// This class never changes data, so the bill counts below stay fixed.
public class BillingApiTests(BillingApiFactory factory) : IClassFixture<BillingApiFactory>
{
    private HttpClient Jane() => factory.SignedInAs(UserRole.Customer, customerId: 1);

    // ---------- authentication ----------

    [Fact]
    public async Task Request_WithoutAToken_Returns401()
    {
        // Act
        var response = await factory.CreateClient().GetAsync("/api/bills");

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Request_WithAGarbageToken_Returns401()
    {
        // Arrange
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "not.a.token");

        // Act
        var response = await client.GetAsync("/api/bills");

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // ---------- authorization: ownership ----------

    [Fact]
    public async Task Customer_ReadingAnotherCustomersBill_Returns403()
    {
        // Act: bill 2 belongs to John (customer 2)
        var response = await Jane().GetAsync("/api/bills/2");

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Customer_ReadingTheirOwnBill_Returns200()
    {
        // Act
        var response = await Jane().GetAsync("/api/bills/1");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Customer_AskingForAnotherCustomersBillList_Returns403()
    {
        // Act
        var response = await Jane().GetAsync("/api/bills?customerId=2");

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Customer_BillList_OnlyContainsTheirOwnBills()
    {
        // Act
        var bills = await Jane().GetFromJsonAsync<JsonElement>("/api/bills");

        // Assert
        Assert.Equal(1, bills.GetArrayLength());
        Assert.Equal(1, bills[0].GetProperty("customerId").GetInt32());
    }

    [Fact]
    public async Task Customer_PayingAnotherCustomersBill_Returns403()
    {
        // Act
        var response = await Jane().PatchAsJsonAsync("/api/bills/2", new { status = "paid" });

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // ---------- admin reports (used by the monolith's /api/admin dashboard) ----------

    // The reports read a SQL Server view and procedure, which SQLite does not have.
    // Their access rules are checked before any database work, so we can still test who gets in.
    [Theory]
    [InlineData("/api/bills/summary")]
    [InlineData("/api/bills/usage-trend")]
    [InlineData("/api/bills/activity")]
    public async Task Reports_RefuseCustomersAndAnonymousCallers(string url)
    {
        // Act
        var asCustomer = await Jane().GetAsync(url);
        var anonymous = await factory.CreateClient().GetAsync(url);

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, asCustomer.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
    }

    [Fact]
    public async Task HealthChecks_ArePublic()
    {
        // Act
        var live = await factory.CreateClient().GetAsync("/health/live");
        var ready = await factory.CreateClient().GetAsync("/health/ready");

        // Assert
        Assert.Equal(HttpStatusCode.OK, live.StatusCode);
        Assert.Equal(HttpStatusCode.OK, ready.StatusCode);
    }
}
