using Microsoft.EntityFrameworkCore;
using ThorntonEnergy.Customers.Api.Data;
using ThorntonEnergy.Customers.Api.Mapping;
using ThorntonEnergy.Customers.Api.Models;
using ThorntonEnergy.Customers.Api.Services;
using ThorntonEnergy.Shared.Data;
using ThorntonEnergy.Shared.Extensions;

var builder = WebApplication.CreateBuilder(args);

// ---------- Services (dependency injection) ----------

// /health/ready checks this service's own database (it calls no other services)
builder.AddDatabaseHealthCheck<CustomersDbContext>();

// The interceptor tells SQL Server who is signed in, so the customer audit trigger can record it
builder.Services.AddScoped<AppUserSessionInterceptor>();

// The Customers service owns its own database (ThorntonCustomers): customers and their rate plans; no other service reads it
builder.Services.AddDbContext<CustomersDbContext>((serviceProvider, options) =>
    options.UseSqlServer(
            builder.Configuration.GetConnectionString("DefaultConnection"),
            sql => sql.EnableRetryOnFailure()) // Azure SQL auto-pauses; retry while it wakes up
        .AddInterceptors(serviceProvider.GetRequiredService<AppUserSessionInterceptor>()));

// Data access and business rules
builder.Services.AddScoped<IRepository<Customer>, EfRepository<CustomersDbContext, Customer>>();
builder.Services.AddScoped<IRepository<RatePlan>, EfRepository<CustomersDbContext, RatePlan>>();
builder.Services.AddScoped<ICustomerService, CustomerService>();
builder.Services.AddScoped<IRatePlanService, RatePlanService>();
builder.Services.AddScoped<ICustomerAccountService, CustomerAccountService>(); // internal: login and register
builder.Services.AddScoped<ICustomerInsightService, CustomerInsightService>();

// AutoMapper copies entities to DTOs and back. The license key is optional (without it, it only logs a reminder).
builder.Services.AddAutoMapper(cfg => cfg.LicenseKey = builder.Configuration["AutoMapper:LicenseKey"], typeof(MappingProfile));

// Same token checks as the monolith, so its login tokens (and its service tokens) work here. This service never issues tokens.
builder.AddJWTAuth();
builder.AddRateLimiting();
builder.AddAPIDefaults();

var app = builder.Build();

// Create the tables, view and trigger, then fill an empty database with the demo rate plans and customers.
// Not in the "Testing" environment: tests use SQLite and create their own data.
if (!app.Environment.IsEnvironment("Testing"))
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<CustomersDbContext>();
    await db.Database.MigrateAsync();
    await CustomersSeeder.SeedAsync(db, app.Environment.ContentRootPath);
}

app.UseMiddleware();
app.Run();

// Lets the test project start the whole app in memory (WebApplicationFactory<Program>)
public partial class Program;
