using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ThorntonEnergy.Shared.Auth;

namespace ThorntonEnergy.Outages.Api.Tests;

// Real HTTP requests against the real Outages service (in memory): who may see what.
// Jane is customer 1 (outage 1), John is customer 2 (outage 2). This class never changes data.
public class OutagesApiTests(OutagesApiFactory factory) : IClassFixture<OutagesApiFactory>
{
    private HttpClient Jane() => factory.SignedInAs(UserRole.Customer, customerId: 1);
    private HttpClient Admin() => factory.SignedInAs(UserRole.Admin);

    [Fact]
    public async Task Outages_WithoutAToken_Returns401()
    {
        // Act
        var response = await factory.CreateClient().GetAsync("/api/outages");

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Outages_CustomerSeesOnlyTheirOwn_AndCannotAskForSomeoneElses()
    {
        // Act
        var own = await Jane().GetFromJsonAsync<JsonElement>("/api/outages");
        var others = await Jane().GetAsync("/api/outages?customerId=2");
        var oneOfJohns = await Jane().GetAsync("/api/outages/2");

        // Assert
        Assert.Equal(1, own.GetArrayLength());
        Assert.Equal(1, own[0].GetProperty("customerId").GetInt32());
        Assert.Equal(HttpStatusCode.Forbidden, others.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, oneOfJohns.StatusCode);
    }

    [Fact]
    public async Task OutageMap_IsAdminOnly()
    {
        // Act
        var forAdmin = await Admin().GetAsync("/api/outages/map");
        var forCustomer = await Jane().GetAsync("/api/outages/map");

        // Assert
        Assert.Equal(HttpStatusCode.OK, forAdmin.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, forCustomer.StatusCode);
    }

    [Fact]
    public async Task OutageMap_JoinsEachOutageWithItsCustomersNameAndLocation()
    {
        // Arrange: the (fake) customers service knows Jane's location; John has no coordinates yet

        // Act
        var pins = await Admin().GetFromJsonAsync<JsonElement>("/api/outages/map");

        // Assert: Jane's outage is pinned with her name and location; John's is left off the map
        var pin = Assert.Single(pins.EnumerateArray());
        Assert.Equal(1, pin.GetProperty("outageId").GetInt32());
        Assert.Equal("Jane Doe", pin.GetProperty("customerName").GetString());
        Assert.Equal(40.2m, pin.GetProperty("latitude").GetDecimal());
        Assert.Equal(-74.5m, pin.GetProperty("longitude").GetDecimal());
    }

    // The reports read a SQL Server view, which SQLite does not have.
    // Their access rules are checked before any database work, so we can still test who gets in.
    [Theory]
    [InlineData("/api/outages/summary")]
    [InlineData("/api/outages/activity")]
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
