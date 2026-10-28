using Microsoft.EntityFrameworkCore;
using ThorntonEnergy.Messages.Api.Data;
using ThorntonEnergy.Messages.Api.Mapping;
using ThorntonEnergy.Messages.Api.Models;
using ThorntonEnergy.Messages.Api.Services;
using ThorntonEnergy.Shared.Data;
using ThorntonEnergy.Shared.Extensions;

var builder = WebApplication.CreateBuilder(args);

// ---------- Services (dependency injection) ----------

// /health/ready checks this service's own database
builder.AddDatabaseHealthCheck<MessagesDbContext>();

// The Messages service owns its own database (ThorntonMessages); no other service reads it
builder.Services.AddDbContext<MessagesDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("DefaultConnection"),
        sql => sql.EnableRetryOnFailure())); // Azure SQL auto-pauses; retry while it wakes up

// Data access and business rules
builder.Services.AddScoped<IRepository<ContactMessage>, EfRepository<MessagesDbContext, ContactMessage>>();
builder.Services.AddScoped<IMessageService, MessageService>();

// AutoMapper copies entities to DTOs and back. The license key is optional (without it, it only logs a reminder).
builder.Services.AddAutoMapper(cfg => cfg.LicenseKey = builder.Configuration["AutoMapper:LicenseKey"], typeof(MappingProfile));

// Same token checks as the monolith, so its login tokens work here. This service never issues tokens.
builder.AddJWTAuth();
builder.AddRateLimiting();
builder.AddAPIDefaults();

var app = builder.Build();

// Create the tables and fill an empty inbox with the demo messages.
// Not in the "Testing" environment: tests use SQLite and create their own data.
if (!app.Environment.IsEnvironment("Testing"))
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<MessagesDbContext>();
    await db.Database.MigrateAsync();
    await MessagesSeeder.SeedAsync(db, app.Environment.ContentRootPath);
}

app.UseMiddleware();
app.Run();

// Lets the test project start the whole app in memory (WebApplicationFactory<Program>)
public partial class Program;
