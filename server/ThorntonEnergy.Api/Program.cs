using Microsoft.EntityFrameworkCore;
using ThorntonEnergy.Api.Data;
using ThorntonEnergy.Api.Services;
using ThorntonEnergy.Shared.Data;
using ThorntonEnergy.Shared.Extensions;

var builder = WebApplication.CreateBuilder(args);

// ---------- Services (dependency injection) ----------

// /health/ready checks this service's database
builder.AddDatabaseHealthCheck<AppDbContext>();

// Logins (Users) and the audit history from before the split. No triggers left, so no session interceptor.
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("DefaultConnection"),
        sql => sql.EnableRetryOnFailure())); // Azure SQL auto-pauses; retry while it wakes up

builder.Services.AddSingleton<ITokenService, TokenService>(); // stateless, so one instance for the whole app

builder.AddJWTAuth();

// Data access: one generic repository class serves every entity
builder.Services.AddScoped(typeof(IRepository<>), typeof(EfRepository<>));

// Business rules live in the services (one instance per request, like the DbContext)
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IAdminInsightService, AdminInsightService>();

// Customers, bills and outages live in their own services; the admin dashboard asks them for their numbers over HTTP
// (passing along the admin's token)
builder.AddServiceClient<ICustomersClient, CustomersClient>("Customers");
builder.AddServiceClient<IBillingClient, BillingClient>("Billing");
builder.AddServiceClient<IOutagesClient, OutagesClient>("Outages");

// Login and register check the customer's account before anyone is signed in, so there is no user token to pass along:
// these calls carry a short-lived service token instead
builder.Services.AddTransient<ServiceTokenHandler>();
builder.AddServiceClient<ICustomerAccounts, CustomerAccountsClient>("Customers", forwardCallerToken: false)
    .AddHttpMessageHandler<ServiceTokenHandler>();

builder.AddRateLimiting();
builder.AddAPIDefaults();

var app = builder.Build();

// Fill an empty database with the v1 demo data (skipped once users exist).
// Not in the "Testing" environment: the seeder uses SQL Server IDENTITY_INSERT, and tests seed their own tiny dataset.
if (!app.Environment.IsEnvironment("Testing"))
{
    using IServiceScope scope = app.Services.CreateScope();
    AppDbContext db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.MigrateAsync(); // Create/Update Tables first.
    await DbSeeder.SeedAsync(db, app.Environment.ContentRootPath);
}

app.UseMiddleware();
app.Run();

// Lets the test project start the whole app in memory (WebApplicationFactory<Program>)
public partial class Program;
