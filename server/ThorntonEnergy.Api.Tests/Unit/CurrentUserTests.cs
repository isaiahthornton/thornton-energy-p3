using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Moq;
using ThorntonEnergy.Shared.Auth;

namespace ThorntonEnergy.Api.Tests.Unit;

// The ownership rules, run against a fake signed-in user (a hand-made ClaimsPrincipal)
public class CurrentUserTests
{
    private static CurrentUser For(string role, int? customerId)
    {
        var claims = new List<Claim>
        {
            new("sub", "1"),
            new("email", "someone@test.com"),
            new(AppClaims.Role, role),
        };
        if (customerId is not null) claims.Add(new Claim(AppClaims.CustomerId, customerId.Value.ToString()));

        var identity = new ClaimsIdentity(claims, "test", nameType: "email", roleType: AppClaims.Role);
        var accessor = new Mock<IHttpContextAccessor>();
        accessor.Setup(a => a.HttpContext).Returns(new DefaultHttpContext { User = new ClaimsPrincipal(identity) });
        return new CurrentUser(accessor.Object);
    }

    [Fact]
    public void Customer_CanOnlyAccessTheirOwnRecords()
    {
        // Arrange
        var user = For("Customer", customerId: 5);

        // Act + Assert
        Assert.False(user.IsAdmin);
        Assert.True(user.CanAccessCustomer(5));
        Assert.False(user.CanAccessCustomer(6));
    }

    [Fact]
    public void Admin_CanAccessAnyCustomer()
    {
        // Arrange
        var user = For("Admin", customerId: null);

        // Act + Assert
        Assert.True(user.IsAdmin);
        Assert.True(user.CanAccessCustomer(99));
    }

    [Fact]
    public void CustomerScope_IsAlwaysTheirOwnId()
    {
        // Arrange
        var user = For("Customer", customerId: 5);

        // Act
        var noFilter = user.TryGetCustomerScope(null, out var scopeWhenNoFilter);
        var ownFilter = user.TryGetCustomerScope(5, out var scopeWhenOwn);

        // Assert
        Assert.True(noFilter);
        Assert.Equal(5, scopeWhenNoFilter);
        Assert.True(ownFilter);
        Assert.Equal(5, scopeWhenOwn);
    }

    [Fact]
    public void CustomerScope_RefusesAskingForSomeoneElse()
    {
        // Arrange
        var user = For("Customer", customerId: 5);

        // Act
        var allowed = user.TryGetCustomerScope(6, out _);

        // Assert
        Assert.False(allowed);
    }

    [Fact]
    public void AdminScope_IsWhateverTheyAskedFor()
    {
        // Arrange
        var admin = For("Admin", customerId: null);

        // Act
        var everyone = admin.TryGetCustomerScope(null, out var allScope);
        var one = admin.TryGetCustomerScope(7, out var oneScope);

        // Assert
        Assert.True(everyone);
        Assert.Null(allScope);   // null = no filter, see everyone
        Assert.True(one);
        Assert.Equal(7, oneScope);
    }

    [Fact]
    public void NoHttpContext_MeansNoAccess()
    {
        // Arrange: a background job or a test with no request
        var accessor = new Mock<IHttpContextAccessor>();
        accessor.Setup(a => a.HttpContext).Returns((HttpContext?)null);
        var user = new CurrentUser(accessor.Object);

        // Act + Assert
        Assert.False(user.IsAdmin);
        Assert.Null(user.CustomerId);
        Assert.Null(user.Email);
        Assert.False(user.CanAccessCustomer(1));
    }
}
