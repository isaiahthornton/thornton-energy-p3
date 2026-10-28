using Microsoft.EntityFrameworkCore;
using ThorntonEnergy.Metering.Api.Data;
using ThorntonEnergy.Metering.Api.Mapping;
using ThorntonEnergy.Metering.Api.Models;
using ThorntonEnergy.Metering.Api.Services;
using ThorntonEnergy.Shared.Data;
using ThorntonEnergy.Shared.Extensions;

var builder = WebApplication.CreateBuilder(args);

// ---------- Services (dependency injection) ----------

// /health/ready checks this service's own database
builder.AddDatabaseHealthCheck<MeteringDbContext>();

// The Metering service owns its own database (ThorntonMetering); no other service reads it
builder.Services.AddDbContext<MeteringDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("DefaultConnection"),
        sql => sql.EnableRetryOnFailure())); // Azure SQL auto-pauses; retry while it wakes up

// Data access and business rules
builder.Services.AddScoped<IRepository<Meter>, EfRepository<MeteringDbContext, Meter>>();
builder.Services.AddScoped<IRepository<MeterReading>, EfRepository<MeteringDbContext, MeterReading>>();
builder.Services.AddScoped<IMeterService, MeterService>();

// AutoMapper copies entities to DTOs and back. The license key is optional (without it, it only logs a reminder).
builder.Services.AddAutoMapper(cfg => cfg.LicenseKey = builder.Configuration["AutoMapper:LicenseKey"], typeof(MappingProfile));

// Same token checks as the monolith, so its login tokens work here. This service never issues tokens.
builder.AddJWTAuth();
builder.AddRateLimiting();
builder.AddAPIDefaults();

var app = builder.Build();

// Create the tables and fill an empty database with the demo meters and readings.
// Not in the "Testing" environment: tests use SQLite and create their own data.
if (!app.Environment.IsEnvironment("Testing"))
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<MeteringDbContext>();
    await db.Database.MigrateAsync();
    await MeteringSeeder.SeedAsync(db, app.Environment.ContentRootPath);
}

app.UseMiddleware();
app.Run();

// Lets the test project start the whole app in memory (WebApplicationFactory<Program>)
public partial class Program;
