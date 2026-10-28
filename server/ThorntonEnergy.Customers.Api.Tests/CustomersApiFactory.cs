using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using ThorntonEnergy.Customers.Api.Data;
using ThorntonEnergy.Shared.Auth;

namespace ThorntonEnergy.Customers.Api.Tests;

// Starts the real Customers service in memory (same controllers, middleware, auth and rate limiting as production),
// but with an in-memory SQLite database instead of SQL Server.
public class CustomersApiFactory : WebApplicationFactory<Program>
{
    // Must match the "Jwt" section in the service's appsettings.json (the monolith uses the same values)
    private const string Issuer = "ThorntonEnergy.Api";
    private const string Audience = "ThorntonEnergy.Client";
    private static readonly string Key = new('t', 48);

    private readonly SqliteConnection _connection = new("DataSource=:memory:");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // "Testing" makes Program.cs skip the SQL Server migration and seeder
        builder.UseEnvironment("Testing");

        // Program.cs refuses to start without these, so give it throw-away values
        builder.UseSetting("Jwt:Key", Key);
        builder.UseSetting("ConnectionStrings:DefaultConnection", "Server=not-used");

        builder.ConfigureTestServices(services =>
        {
            // Take out the SQL Server DbContext Program.cs registered...
            services.RemoveAll<DbContextOptions<CustomersDbContext>>();
            services.RemoveAll(typeof(IDbContextOptionsConfiguration<CustomersDbContext>));
            services.RemoveAll<CustomersDbContext>();

            // ...and put in one that talks to the shared in-memory SQLite connection
            _connection.Open();
            services.AddDbContext<CustomersDbContext>(options => options.UseSqlite(_connection));
        });
    }

    // Build the tables and the tiny test dataset once, before the first request
    protected override IHost CreateHost(IHostBuilder builder)
    {
        var host = base.CreateHost(builder);

        using var scope = host.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CustomersDbContext>();
        db.Database.EnsureCreated();
        TestData.Seed(db);

        return host;
    }

    // This service has no login endpoint (only the monolith issues tokens), so tests sign their own,
    // with the same claims the monolith's TokenService writes.
    public HttpClient SignedInAs(UserRole role, int? customerId = null) =>
        WithToken(role.ToString(), $"{role.ToString().ToLower()}@test.com", customerId);

    // What the monolith's login and register send: a token for a service, not a person (no email, no customer)
    public HttpClient SignedInAsService() => WithToken(AppRoles.Service, email: null, customerId: null);

    private HttpClient WithToken(string role, string? email, int? customerId)
    {
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, "1"),
            new(AppClaims.Role, role),
        };
        if (email is not null) claims.Add(new(JwtRegisteredClaimNames.Email, email));
        if (customerId is not null) claims.Add(new(AppClaims.CustomerId, customerId.Value.ToString()));

        var token = new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Issuer = Issuer,
            Audience = Audience,
            Expires = DateTime.UtcNow.AddMinutes(10),
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(Key)), SecurityAlgorithms.HmacSha256),
        });

        var client = CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", token);
        return client;
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing) _connection.Dispose();
    }
}
