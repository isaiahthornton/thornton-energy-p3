using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using ThorntonEnergy.Api.Data;
using ThorntonEnergy.Api.Services;
using ThorntonEnergy.Api.Tests.Helpers;

namespace ThorntonEnergy.Api.Tests.Integration;

// Starts the real API in memory (same controllers, middleware, auth and rate limiting as production),
// but with an in-memory SQLite database instead of Azure SQL.
public class ApiFactory : WebApplicationFactory<Program>
{
    private readonly SqliteConnection _connection = new("DataSource=:memory:");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // "Testing" makes Program.cs skip the SQL Server seeder
        builder.UseEnvironment("Testing");

        // Program.cs refuses to start without these, so give it throw-away values
        builder.UseSetting("Jwt:Key", new string('t', 48));
        builder.UseSetting("ConnectionStrings:DefaultConnection", "Server=not-used");

        builder.ConfigureTestServices(services =>
        {
            // Take out the SQL Server DbContext Program.cs registered...
            services.RemoveAll<DbContextOptions<AppDbContext>>();
            services.RemoveAll(typeof(IDbContextOptionsConfiguration<AppDbContext>));
            services.RemoveAll<AppDbContext>();

            // ...and put in one that talks to the shared in-memory SQLite connection
            _connection.Open();
            services.AddScoped<AppDbContext>(_ => TestDb.Create(_connection));

            // No HTTP calls to the Customers, Billing or Outages services in tests
            services.RemoveAll<ICustomerAccounts>();
            services.AddSingleton<ICustomerAccounts, FakeCustomerAccounts>();
            services.RemoveAll<ICustomersClient>();
            services.AddSingleton<ICustomersClient, FakeCustomersClient>();
            services.RemoveAll<IBillingClient>();
            services.AddSingleton<IBillingClient, FakeBillingClient>();
            services.RemoveAll<IOutagesClient>();
            services.AddSingleton<IOutagesClient, FakeOutagesClient>();
        });
    }

    // Build the tables and the tiny test dataset once, before the first request
    protected override IHost CreateHost(IHostBuilder builder)
    {
        var host = base.CreateHost(builder);

        using var scope = host.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Database.EnsureCreated();
        TestData.Seed(db);

        return host;
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing) _connection.Dispose();
    }
}
