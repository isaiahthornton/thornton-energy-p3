using Microsoft.EntityFrameworkCore;
using ThorntonEnergy.Billing.Api.Data;
using ThorntonEnergy.Billing.Api.Mapping;
using ThorntonEnergy.Billing.Api.Models;
using ThorntonEnergy.Billing.Api.Services;
using ThorntonEnergy.Shared.Data;
using ThorntonEnergy.Shared.Extensions;

var builder = WebApplication.CreateBuilder(args);

// ---------- Services (dependency injection) ----------

// /health/ready checks this service's own database (not the services it calls, so one outage doesn't cascade)
builder.AddDatabaseHealthCheck<BillingDbContext>();

// The interceptor tells SQL Server who is signed in, so the bill audit trigger can record it
builder.Services.AddScoped<AppUserSessionInterceptor>();

// The Billing service owns its own database (ThorntonBilling); no other service reads it
builder.Services.AddDbContext<BillingDbContext>((serviceProvider, options) =>
    options.UseSqlServer(
            builder.Configuration.GetConnectionString("DefaultConnection"),
            sql => sql.EnableRetryOnFailure()) // Azure SQL auto-pauses; retry while it wakes up
        .AddInterceptors(serviceProvider.GetRequiredService<AppUserSessionInterceptor>()));

// Data access and business rules
builder.Services.AddScoped<IRepository<Bill>, EfRepository<BillingDbContext, Bill>>();
builder.Services.AddScoped<IBillService, BillService>();
builder.Services.AddScoped<IBillingInsightService, BillingInsightService>();

// Customers and rate plans live in another service; new bills are priced from them over HTTP
builder.AddServiceClient<ICustomerDirectory, CustomerDirectoryClient>("Customers");

// AutoMapper copies entities to DTOs and back. The license key is optional (without it, it only logs a reminder).
builder.Services.AddAutoMapper(cfg => cfg.LicenseKey = builder.Configuration["AutoMapper:LicenseKey"], typeof(MappingProfile));

// Same token checks as the monolith, so its login tokens work here. This service never issues tokens.
builder.AddJWTAuth();
builder.AddRateLimiting();
builder.AddAPIDefaults();

var app = builder.Build();

// Create the tables, view, procedure and trigger, then fill an empty database with the demo bills.
// Not in the "Testing" environment: tests use SQLite and create their own data.
if (!app.Environment.IsEnvironment("Testing"))
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<BillingDbContext>();
    await db.Database.MigrateAsync();
    await BillingSeeder.SeedAsync(db, app.Environment.ContentRootPath);
}

app.UseMiddleware();
app.Run();

// Lets the test project start the whole app in memory (WebApplicationFactory<Program>)
public partial class Program;
