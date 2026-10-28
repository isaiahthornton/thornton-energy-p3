using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using ThorntonEnergy.Api.Models;
using ThorntonEnergy.Api.Services;
using ThorntonEnergy.Shared.Auth;

namespace ThorntonEnergy.Api.Tests.Unit;

// A clock that only moves when the test says so
public class FakeTimeProvider(DateTimeOffset now) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => now;
}

public class TokenServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 6, 15, 12, 0, 0, TimeSpan.Zero);

    private static TokenService CreateService() => new(
        Options.Create(new JwtOptions
        {
            Issuer = "test-issuer",
            Audience = "test-audience",
            Key = new string('k', 40), // HS256 needs a long enough key
            ExpiresMinutes = 30,
        }),
        new FakeTimeProvider(Now));

    [Fact]
    public void CreateToken_ContainsTheUsersClaims()
    {
        // Arrange
        var user = new User { Id = 7, Email = "jane@test.com", Role = UserRole.Customer, CustomerId = 3 };

        // Act
        var token = new JsonWebTokenHandler().ReadJsonWebToken(CreateService().CreateToken(user).Token);

        // Assert
        Assert.Equal("7", token.GetClaim(JwtRegisteredClaimNames.Sub).Value);
        Assert.Equal("jane@test.com", token.GetClaim(JwtRegisteredClaimNames.Email).Value);
        Assert.Equal("Customer", token.GetClaim(AppClaims.Role).Value);
        Assert.Equal("3", token.GetClaim(AppClaims.CustomerId).Value);
        Assert.Equal("test-issuer", token.Issuer);
    }

    [Fact]
    public void CreateServiceToken_HasTheServiceRole_NoPersonalClaims_AndExpiresAfterFiveMinutes()
    {
        // Act
        var serviceToken = CreateService().CreateServiceToken();
        var token = new JsonWebTokenHandler().ReadJsonWebToken(serviceToken.Token);

        // Assert: only [Authorize(Roles = "Service")] accepts it, and it can never pass an ownership check
        Assert.Equal(AppRoles.Service, token.GetClaim(AppClaims.Role).Value);
        Assert.False(token.TryGetClaim(JwtRegisteredClaimNames.Email, out _));
        Assert.False(token.TryGetClaim(AppClaims.CustomerId, out _));
        Assert.Equal(Now.UtcDateTime.AddMinutes(5), serviceToken.ExpiresAt);
    }

    [Fact]
    public void CreateToken_AdminHasNoCustomerIdClaim()
    {
        // Arrange
        var admin = new User { Id = 1, Email = "admin@test.com", Role = UserRole.Admin, CustomerId = null };

        // Act
        var token = new JsonWebTokenHandler().ReadJsonWebToken(CreateService().CreateToken(admin).Token);

        // Assert
        Assert.Equal("Admin", token.GetClaim(AppClaims.Role).Value);
        Assert.False(token.TryGetClaim(AppClaims.CustomerId, out _));
    }

    [Fact]
    public void CreateToken_ExpiresAfterTheConfiguredMinutes()
    {
        // Arrange
        var user = new User { Id = 1, Email = "a@test.com", Role = UserRole.Customer };

        // Act
        var result = CreateService().CreateToken(user);
        var token = new JsonWebTokenHandler().ReadJsonWebToken(result.Token);

        // Assert: the fake clock says 12:00, and the lifetime is 30 minutes
        Assert.Equal(Now.UtcDateTime.AddMinutes(30), result.ExpiresAt);
        Assert.Equal(result.ExpiresAt, token.ValidTo);
    }

    [Fact]
    public async Task CreateToken_IsValidWithTheRightKeyAndRejectedWithAWrongOne()
    {
        // Arrange
        var service = CreateService();
        var token = service.CreateToken(new User { Id = 1, Email = "a@test.com", Role = UserRole.Customer }).Token;
        var handler = new JsonWebTokenHandler();

        TokenValidationParametersFactory.Create(new string('k', 40), out var good);
        TokenValidationParametersFactory.Create(new string('x', 40), out var bad);

        // Act
        var goodResult = await handler.ValidateTokenAsync(token, good);
        var badResult = await handler.ValidateTokenAsync(token, bad);

        // Assert
        Assert.True(goodResult.IsValid);
        Assert.False(badResult.IsValid);
    }
}

// Builds the same checks the API runs on every request (signature, issuer, audience), but ignores the clock
// because the fake clock is in the past.
internal static class TokenValidationParametersFactory
{
    public static void Create(string key, out Microsoft.IdentityModel.Tokens.TokenValidationParameters parameters)
    {
        parameters = new Microsoft.IdentityModel.Tokens.TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = "test-issuer",
            ValidateAudience = true,
            ValidAudience = "test-audience",
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new Microsoft.IdentityModel.Tokens.SymmetricSecurityKey(System.Text.Encoding.UTF8.GetBytes(key)),
            ValidateLifetime = false,
        };
    }
}
