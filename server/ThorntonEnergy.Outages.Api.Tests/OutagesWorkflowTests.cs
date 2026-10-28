using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ThorntonEnergy.Shared.Auth;

namespace ThorntonEnergy.Outages.Api.Tests;

// Whole workflows through HTTP: report, change, read back. Ported from the monolith's outage tests.
// Its own test class, so it gets its own in-memory app and cannot disturb the counts in OutagesApiTests.
public class OutagesWorkflowTests(OutagesApiFactory factory) : IClassFixture<OutagesApiFactory>
{
    private HttpClient Jane() => factory.SignedInAs(UserRole.Customer, customerId: 1);
    private HttpClient Admin() => factory.SignedInAs(UserRole.Admin);

    private async Task<int> ReportForJohnAsync()
    {
        var created = await Admin().PostAsJsonAsync("/api/outages", new { customerId = 2, address = "2 Main St", description = "Dark" });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        return (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetInt32();
    }

    [Fact]
    public async Task Customer_OutageReport_IsSavedUnderTheirOwnAccount()
    {
        // Act: Jane (customer 1) claims to be customer 2 and sends a "resolved" status
        var response = await Jane().PostAsJsonAsync("/api/outages",
            new { customerId = 2, address = "1 Main St", description = "Lights are out", status = "resolved" });

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var outage = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(1, outage.GetProperty("customerId").GetInt32());
        Assert.Equal("reported", outage.GetProperty("status").GetString());
    }

    [Fact]
    public async Task Admin_ResolvingAnOutage_StampsTheResolveTimeOnTheServer()
    {
        // Arrange
        var id = await ReportForJohnAsync();

        // Act
        var resolved = await Admin().PatchAsJsonAsync($"/api/outages/{id}", new { status = "resolved" });
        var reopened = await Admin().PatchAsJsonAsync($"/api/outages/{id}", new { status = "in_progress" });

        // Assert
        var resolvedBody = await resolved.Content.ReadFromJsonAsync<JsonElement>();
        var reopenedBody = await reopened.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(JsonValueKind.String, resolvedBody.GetProperty("timeResolved").ValueKind);
        Assert.Equal(JsonValueKind.Null, reopenedBody.GetProperty("timeResolved").ValueKind);
    }

    [Fact]
    public async Task Admin_ReportingAnOutageWithoutACustomer_Returns400()
    {
        // Act
        var response = await Admin().PostAsJsonAsync("/api/outages", new { address = "Somewhere", description = "Dark" });

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Admin_ReportingForAnUnknownCustomer_Returns400()
    {
        // Act: the customers service does not know customer 99
        var response = await Admin().PostAsJsonAsync("/api/outages", new { customerId = 99, address = "Nowhere", description = "Dark" });

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Customer_CannotUpdateOutageStatus_OrReadAnotherCustomersOutage()
    {
        // Arrange
        var id = await ReportForJohnAsync();

        // Act
        var patch = await Jane().PatchAsJsonAsync($"/api/outages/{id}", new { status = "resolved" });
        var read = await Jane().GetAsync($"/api/outages/{id}");

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, patch.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, read.StatusCode);
    }
}
