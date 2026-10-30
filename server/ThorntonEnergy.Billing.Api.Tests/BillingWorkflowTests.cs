using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ThorntonEnergy.Shared.Auth;

namespace ThorntonEnergy.Billing.Api.Tests;

// Whole workflows through HTTP: create, change, read back. Ported from the monolith's ApiWorkflowTests bill tests.
// Its own test class, so it gets its own in-memory app and cannot disturb the counts in BillingApiTests.
public class BillingWorkflowTests(BillingApiFactory factory) : IClassFixture<BillingApiFactory>
{
    private HttpClient Jane() => factory.SignedInAs(UserRole.Customer, customerId: 1);
    private HttpClient Admin() => factory.SignedInAs(UserRole.Admin);

    [Fact]
    public async Task Admin_CreatesABill_ThePriceComesFromTheRatePlan_AndCustomerCanPayItOnce()
    {
        // Arrange
        var admin = Admin();
        var jane = Jane();

        // Act: the browser sends a wrong total on purpose; the server must ignore it
        var created = await admin.PostAsJsonAsync("/api/bills",
            new { customerId = 1, periodStart = "2030-01-01", periodEnd = "2030-01-31", kwhUsed = 100, amountDue = 1, status = "paid" });
        var bill = await created.Content.ReadFromJsonAsync<JsonElement>();
        var id = bill.GetProperty("id").GetInt32();

        var firstPay = await jane.PatchAsJsonAsync($"/api/bills/{id}", new { status = "paid" });
        var secondPay = await jane.PatchAsJsonAsync($"/api/bills/{id}", new { status = "paid" });

        // Assert: plan is 0.10/kWh + 10.00 fee (from the fake customers service), so 100 kWh = 20.00
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        Assert.Equal(20.00m, bill.GetProperty("amountDue").GetDecimal());
        Assert.Equal("unpaid", bill.GetProperty("status").GetString());
        Assert.Equal(HttpStatusCode.OK, firstPay.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, secondPay.StatusCode);
    }

    [Fact]
    public async Task Admin_CreatingABillForAnUnknownCustomer_Returns400()
    {
        // Act: the customers service does not know customer 99
        var response = await Admin().PostAsJsonAsync("/api/bills",
            new { customerId = 99, periodStart = "2030-02-01", periodEnd = "2030-02-28", kwhUsed = 5 });

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Customer_CannotCreateABill()
    {
        // Act
        var response = await Jane().PostAsJsonAsync("/api/bills",
            new { customerId = 1, periodStart = "2030-03-01", periodEnd = "2030-03-31", kwhUsed = 1 });

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Admin_CreatingADuplicateBill_Returns409()
    {
        // Act: customer 2 already has a bill starting 2026-01-01
        var response = await Admin().PostAsJsonAsync("/api/bills",
            new { customerId = 2, periodStart = "2026-01-01", periodEnd = "2026-01-31", kwhUsed = 5 });

        // Assert
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task OldUnpaidBill_ComesBackAsOverdue()
    {
        // Act: Jane's bill 1 was due in early 2026 and is still unpaid
        var bill = await Jane().GetFromJsonAsync<JsonElement>("/api/bills/1");

        // Assert
        Assert.Equal("overdue", bill.GetProperty("status").GetString());
    }
}
