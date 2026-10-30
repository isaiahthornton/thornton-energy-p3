using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using ThorntonEnergy.Shared.Middleware;
namespace ThorntonEnergy.Shared.Extensions;

public static class MiddlewareInit
{
    
    // ---------- HTTP request pipeline (order matters) ----------
    public static WebApplication UseMiddleware(this WebApplication app)
    {
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
        return app;
    }
}