using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using ThorntonEnergy.Api.Models;
using ThorntonEnergy.Shared.Auth;

namespace ThorntonEnergy.Api.Services;

public class TokenService(IOptions<JwtOptions> options, TimeProvider clock) : ITokenService
{
    private readonly JwtOptions _jwt = options.Value;

    public AuthToken CreateToken(User user)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        var expiresAt = now.AddMinutes(_jwt.ExpiresMinutes);

        // Claims = facts about the user. The payload is only Base64Url-encoded (readable by anyone), so no secrets.
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Email, user.Email),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()), // unique id for this token
            new(AppClaims.Role, user.Role.ToString()),
        };
        if (user.CustomerId is not null)
            claims.Add(new(AppClaims.CustomerId, user.CustomerId.Value.ToString()));

        var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwt.Key));

        var descriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Issuer = _jwt.Issuer,
            Audience = _jwt.Audience,
            IssuedAt = now,
            NotBefore = now,
            Expires = expiresAt,
            // HS256 = HMAC-SHA256 signature with our secret key: changing any part of the token breaks it
            SigningCredentials = new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256),
        };

        return new AuthToken(new JsonWebTokenHandler().CreateToken(descriptor), expiresAt);
    }
}