using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ThorntonEnergy.Shared.Auth;

namespace ThorntonEnergy.Customers.Api.Tests;

// Whole workflows through HTTP: change, read back. Ported from the monolith's customer and rate plan tests.
// Its own test class, so it gets its own in-memory app and cannot disturb the counts in CustomersApiTests.
public class CustomersWorkflowTests(CustomersApiFactory factory) : IClassFixture<CustomersApiFactory>
{
    private HttpClient Jane() => factory.SignedInAs(UserRole.Customer, customerId: 1);
    private HttpClient Admin() => factory.SignedInAs(UserRole.Admin);

    // ---------- customers ----------

    [Fact]
    public async Task Customer_CanChangeOwnRatePlan_ButNotStatus_AndNotSomeoneElsesRecord()
    {
        // Arrange
        var jane = Jane();

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
    public async Task Admin_SuspendingACustomer_IsWhatTheMonolithsLoginSees()
    {
        // Act: suspend John, read his status the way the monolith's login does, then restore him
        var suspend = await Admin().PatchAsJsonAsync("/api/customers/2", new { status = "suspended" });
        var seenByLogin = await factory.SignedInAsService().GetFromJsonAsync<JsonElement>("/api/internal/customers/2");
        var restore = await Admin().PatchAsJsonAsync("/api/customers/2", new { status = "active" });

        // Assert
        Assert.Equal(HttpStatusCode.OK, suspend.StatusCode);
        Assert.Equal("suspended", seenByLogin.GetProperty("status").GetString());
        Assert.Equal(HttpStatusCode.OK, restore.StatusCode);
    }

    // ---------- rate plans ----------

    [Fact]
    public async Task RatePlans_AdminCanCreateUpdateAndDelete_ButNotWhileCustomersAreOnThePlan()
    {
        // Arrange
        var admin = Admin();
        var plan = new { name = "Night Owl", pricePerKwh = 0.08, monthlyFee = 5, description = "Cheap at night" };

        // Act
        var created = await admin.PostAsJsonAsync("/api/ratePlans", plan);
        var id = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetInt32();
        var replaced = await admin.PutAsJsonAsync($"/api/ratePlans/{id}", new { name = "Night Owl 2", pricePerKwh = 0.07, monthlyFee = 5, description = "Cheaper" });
        var deletedInUse = await admin.DeleteAsync("/api/ratePlans/1"); // customers are on plan 1
        var deleted = await admin.DeleteAsync($"/api/ratePlans/{id}");
        var gone = await admin.GetAsync($"/api/ratePlans/{id}");
        var byCustomer = await Jane().PostAsJsonAsync("/api/ratePlans", plan);

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
        // Act
        var response = await Admin().PostAsJsonAsync("/api/ratePlans", new { name = "Bad", pricePerKwh = -1, monthlyFee = 5, description = "x" });

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
