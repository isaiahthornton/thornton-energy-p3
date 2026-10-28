using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ThorntonEnergy.Shared.Common;
using ThorntonEnergy.Shared.Middleware;
using ThorntonEnergy.Shared.OpenApi;

namespace ThorntonEnergy.Shared.Extensions;
public static class APIDefaults
{
    public static WebApplicationBuilder AddAPIDefaults(this WebApplicationBuilder builder)
    {
        // Timekeeping
        builder.Services.AddSingleton(TimeProvider.System);
        builder.Services.AddSingleton<IUtilityClock, UtilityClock>();  // "now" and "today" in the utility time zone
        
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
        builder.AddCORS();
        builder.AddHealthChecks();
        return builder;
    }

    private static WebApplicationBuilder AddCORS(this WebApplicationBuilder builder)
    {
        // Which websites may call this API from a browser:
        // appsettings.json has localhost; Azure adds the live front end (Cors__AllowedOrigins__1)
        var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
        builder.Services.AddCors(options =>
            options.AddPolicy("Frontend", policy =>
                policy.WithOrigins(allowedOrigins)
                    .AllowAnyHeader()
                    .AllowAnyMethod()
                    .WithExposedHeaders(RequestLoggingMiddleware.HeaderName))); // lets the browser read the correlation id

        return builder;
    }

    private static WebApplicationBuilder AddHealthChecks(this WebApplicationBuilder builder)
    {
        // Kubernetes probes: /health/live and /health/ready (mapped in MiddlewareInit).
        // No checks here, so "live" only means the process answers; each service adds its own database check.
        builder.Services.AddHealthChecks();
        return builder;
    }

    // Each service names its own DbContext: builder.AddDatabaseHealthCheck<AppDbContext>()
    // Tagged "ready" so only /health/ready runs it (a paused Azure SQL must not restart the pod).
    public static WebApplicationBuilder AddDatabaseHealthCheck<TContext>(this WebApplicationBuilder builder)
        where TContext : DbContext
    {
        builder.Services.AddHealthChecks()
            .AddDbContextCheck<TContext>(tags: ["ready"]);
        return builder;
    }
}