using Microsoft.IdentityModel.JsonWebTokens;
using System.Text;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using ThorntonEnergy.Shared.Auth;

namespace ThorntonEnergy.Shared.Extensions;

public static class AuthExtensions
{
    public static WebApplicationBuilder AddJWTAuth(this WebApplicationBuilder builder)
    {
        // JWT settings: Issuer/Audience/ExpiresMinutes from appsettings.json; Key from user secrets (local) or Jwt__Key (Azure)
        var jwtSection = builder.Configuration.GetSection(JwtOptions.SectionName);
        var jwt = jwtSection.Get<JwtOptions>() ?? new JwtOptions();
        if (jwt.Key.Length < 32)
            throw new InvalidOperationException(
                "Jwt:Key is missing or shorter than 32 characters. Set it with 'dotnet user-secrets' locally or the Jwt__Key app setting in Azure.");
        builder.Services.Configure<JwtOptions>(jwtSection);

        // Who is calling? (reads the JWT of the current request)
        builder.Services.AddHttpContextAccessor();
        builder.Services.AddScoped<ICurrentUser, CurrentUser>();


        // Authentication: read "Authorization: Bearer <token>" and verify signature, issuer, audience and expiry on every request
        builder.Services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.MapInboundClaims = false; // keep the short claim names we wrote ("sub", "email", "role")
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = jwt.Issuer,
                    ValidateAudience = true,
                    ValidAudience = jwt.Audience,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Key)),
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.FromMinutes(1),
                    NameClaimType = JwtRegisteredClaimNames.Email,
                    RoleClaimType = AppClaims.Role,
                };
            });

        // Authorization: secure by default. Every endpoint needs a logged-in user unless it says [AllowAnonymous];
        // roles ([Authorize(Roles = ...)]) and ownership checks (AppControllerBase) narrow it further.
        builder.Services.AddAuthorization(options =>
        {
            options.FallbackPolicy = new AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .Build();
        });

        return builder;
    }

    public static WebApplicationBuilder AddRateLimiting(this WebApplicationBuilder builder)
    {
        // Rate limiting: at most 10 login/register/contact attempts per minute from one IP address (then 429)
        builder.Services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.AddPolicy(RateLimitPolicies.PublicForms, httpContext =>
                RateLimitPartition.GetFixedWindowLimiter(
                    partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                    factory: _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = 10,
                        Window = TimeSpan.FromMinutes(1),
                        QueueLimit = 0,
                    }));
            options.OnRejected = async (context, cancellationToken) =>
                await context.HttpContext.Response.WriteAsJsonAsync(
                    new { message = "Too many attempts. Please wait a minute and try again." }, cancellationToken);
        });

        return builder;
    }
}