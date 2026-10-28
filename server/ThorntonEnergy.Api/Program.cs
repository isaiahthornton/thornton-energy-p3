using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using ThorntonEnergy.Api.Auth;
using ThorntonEnergy.Api.Common;
using ThorntonEnergy.Api.Data;
using ThorntonEnergy.Api.Mapping;
using ThorntonEnergy.Api.Middleware;
using ThorntonEnergy.Api.OpenApi;
using ThorntonEnergy.Api.Services;

var builder = WebApplication.CreateBuilder(args);

// ---------- Services (dependency injection) ----------

// Kubernetes Probes Health Checks.
builder.Services.AddHealthChecks()
       .AddDbContextCheck<AppDbContext>(tags: ["ready"]); 

// The interceptor tells SQL Server who is signed in, so the audit triggers can record it
builder.Services.AddScoped<AppUserSessionInterceptor>();
builder.Services.AddDbContext<AppDbContext>((serviceProvider, options) =>
    options.UseSqlServer(
            builder.Configuration.GetConnectionString("DefaultConnection"),
            sql => sql.EnableRetryOnFailure()) // Azure SQL auto-pauses; retry while it wakes up
        .AddInterceptors(serviceProvider.GetRequiredService<AppUserSessionInterceptor>()));

// JWT settings: Issuer/Audience/ExpiresMinutes from appsettings.json; Key from user secrets (local) or Jwt__Key (Azure)
var jwtSection = builder.Configuration.GetSection(JwtOptions.SectionName);
var jwt = jwtSection.Get<JwtOptions>() ?? new JwtOptions();
if (jwt.Key.Length < 32)
    throw new InvalidOperationException(
        "Jwt:Key is missing or shorter than 32 characters. Set it with 'dotnet user-secrets' locally or the Jwt__Key app setting in Azure.");
builder.Services.Configure<JwtOptions>(jwtSection);

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<IUtilityClock, UtilityClock>();  // "now" and "today" in the utility time zone
builder.Services.AddSingleton<ITokenService, TokenService>(); // stateless, so one instance for the whole app

// Who is calling? (reads the JWT of the current request)
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUser, CurrentUser>();

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

// Which websites may call this API from a browser:
// appsettings.json has localhost; Azure adds the live front end (Cors__AllowedOrigins__1)
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
builder.Services.AddCors(options =>
    options.AddPolicy("Frontend", policy =>
        policy.WithOrigins(allowedOrigins)
              .AllowAnyHeader()
              .AllowAnyMethod()
              .WithExposedHeaders(RequestLoggingMiddleware.HeaderName))); // lets the browser read the correlation id

builder.Services
    .AddControllers(options =>
        // Navigation properties (Bill.Customer, etc.) are filled by EF, not sent by Angular
        options.SuppressImplicitRequiredAttributeForNonNullableReferenceTypes = true)
    .AddJsonOptions(options =>
        // Enums go out and come in as "in_progress", "paid", etc., exactly what Angular uses
        options.JsonSerializerOptions.Converters.Add(
            new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower)));

// OpenAPI document (+ the Authorize button for JWTs in Swagger UI)
builder.Services.AddOpenApi(options => options.AddDocumentTransformer<BearerSecuritySchemeTransformer>());

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

// ---------- HTTP request pipeline (order matters) ----------

// First, so every request gets a correlation id and every error is caught
app.UseMiddleware<RequestLoggingMiddleware>();
app.UseMiddleware<ExceptionHandlingMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi().AllowAnonymous(); // the API description itself stays public in development
    app.UseSwaggerUI(options => options.SwaggerEndpoint("/openapi/v1.json", "Thornton Energy API"));
}

// In a container, HTTPS is terminated, so the app only speaks HTTP.
if (!app.Configuration.GetValue<bool>("DOTNET_RUNNING_IN_CONTAINER"))
    app.UseHttpsRedirection();

app.UseCors("Frontend");

app.UseAuthentication(); // 1. Who are you? (reads and validates the JWT)
app.UseAuthorization();  // 2. Are you allowed? (roles, [AllowAnonymous], fallback policy)
app.UseRateLimiter();    // 3. Too many attempts? (only endpoints with [EnableRateLimiting])

app.MapControllers();

/* Health Checking.
 * Live:  "Is the service awake?"
 * Ready: "Is the database awake?"
 */
app.MapHealthChecks("/health/live", new HealthCheckOptions{Predicate = _ => false }).AllowAnonymous();
app.MapHealthChecks("/health/ready", new HealthCheckOptions{Predicate = check => check.Tags.Contains("ready")}).AllowAnonymous();
app.Run();

// Lets the test project start the whole app in memory (WebApplicationFactory<Program>)
public partial class Program;
