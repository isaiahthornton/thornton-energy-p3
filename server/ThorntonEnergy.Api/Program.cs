using Microsoft.EntityFrameworkCore;
using ThorntonEnergy.Api.Data;
using ThorntonEnergy.Api.Mapping;
using ThorntonEnergy.Api.Services;
using ThorntonEnergy.Shared.Data;
using ThorntonEnergy.Shared.Extensions;

var builder = WebApplication.CreateBuilder(args);

// ---------- Services (dependency injection) ----------

// /health/ready checks this service's database
builder.AddDatabaseHealthCheck<AppDbContext>();

// The interceptor tells SQL Server who is signed in, so the audit triggers can record it
builder.Services.AddScoped<AppUserSessionInterceptor>();
builder.Services.AddDbContext<AppDbContext>((serviceProvider, options) =>
    options.UseSqlServer(
            builder.Configuration.GetConnectionString("DefaultConnection"),
            sql => sql.EnableRetryOnFailure()) // Azure SQL auto-pauses; retry while it wakes up
        .AddInterceptors(serviceProvider.GetRequiredService<AppUserSessionInterceptor>()));

builder.Services.AddSingleton<ITokenService, TokenService>(); // stateless, so one instance for the whole app

builder.AddJWTAuth();

// Data access: one generic repository class serves every entity
builder.Services.AddScoped(typeof(IRepository<>), typeof(EfRepository<>));

// Business rules live in the services (one instance per request, like the DbContext)
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IBillService, BillService>();
builder.Services.AddScoped<ICustomerService, CustomerService>();
builder.Services.AddScoped<IMeterService, MeterService>();
builder.Services.AddScoped<IOutageService, OutageService>();
builder.Services.AddScoped<IMessageService, MessageService>();
builder.Services.AddScoped<IRatePlanService, RatePlanService>();
builder.Services.AddScoped<IAdminInsightService, AdminInsightService>();

// AutoMapper copies entities to DTOs and back. The license key is optional (without it, it only logs a reminder).
builder.Services.AddAutoMapper(cfg => cfg.LicenseKey = builder.Configuration["AutoMapper:LicenseKey"], typeof(MappingProfile));

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
