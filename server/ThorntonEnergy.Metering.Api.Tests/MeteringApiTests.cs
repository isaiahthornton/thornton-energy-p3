using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ThorntonEnergy.Shared.Auth;

namespace ThorntonEnergy.Metering.Api.Tests;

// Real HTTP requests against the real Metering service (in memory): the rules as a client sees them.
// Ported from the monolith's ApiWorkflowTests meter tests. Jane is customer 1 and owns meter 1.
public class MeteringApiTests(MeteringApiFactory factory) : IClassFixture<MeteringApiFactory>
{
    private HttpClient Jane() => factory.SignedInAs(UserRole.Customer, customerId: 1);
    private HttpClient Admin() => factory.SignedInAs(UserRole.Admin);

    [Fact]
    public async Task Meters_WithoutAToken_Returns401()
    {
        // Act
        var response = await factory.CreateClient().GetAsync("/api/meters");

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Meters_CustomerSeesOnlyTheirOwn_AdminSeesAll()
    {
        // Act
        var janeMeters = await Jane().GetFromJsonAsync<JsonElement>("/api/meters");
        var adminMeters = await Admin().GetFromJsonAsync<JsonElement>("/api/meters");
        var forbidden = await Jane().GetAsync("/api/meters?customerId=2");

        // Assert
        Assert.Equal(1, janeMeters.GetArrayLength());
        Assert.Equal(1, adminMeters.GetArrayLength());
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
    }

    [Fact]
    public async Task MeterReadings_LowerValueIsRejected_HigherIsAccepted_AndOnlyAdminsMayAdd()
    {
        // Arrange: meter 1 already reads 1000 on 2026-01-01
        var admin = Admin();
        var jane = Jane();

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
    public async Task MeterReadings_AnotherCustomerCannotSeeJanesReadings()
    {
        // Act: customer 2 owns no meters, so their reading list is empty and Jane's reading is off limits
        var john = factory.SignedInAs(UserRole.Customer, customerId: 2);
        var list = await john.GetFromJsonAsync<JsonElement>("/api/meterReadings?meterId=1");
        var one = await john.GetAsync("/api/meterReadings/1");

        // Assert
        Assert.Equal(0, list.GetArrayLength());
        Assert.Equal(HttpStatusCode.Forbidden, one.StatusCode);
    }

    [Fact]
    public async Task MeterStatus_CanBeChangedByAnAdmin_ButNotByACustomer()
    {
        // Act
        var byJane = await Jane().PatchAsJsonAsync("/api/meters/1", new { status = "inactive" });
        var byAdmin = await Admin().PatchAsJsonAsync("/api/meters/1", new { status = "inactive" });

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, byJane.StatusCode);
        Assert.Equal(HttpStatusCode.OK, byAdmin.StatusCode);
        Assert.Equal("inactive", (await byAdmin.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("status").GetString());
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
