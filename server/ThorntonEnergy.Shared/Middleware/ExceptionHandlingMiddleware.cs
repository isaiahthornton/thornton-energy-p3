using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ThorntonEnergy.Shared.Middleware;

// Last line of defence: anything a controller or service did not handle ends up here and becomes
// a clean JSON error (ProblemDetails) instead of a crash page or a stack trace.
public class ExceptionHandlingMiddleware(
    RequestDelegate next,
    ILogger<ExceptionHandlingMiddleware> logger,
    IHostEnvironment environment)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            // Someone else changed the same row first (see Bill.RowVersion): ask the user to reload and retry
            logger.LogWarning(ex, "Concurrency conflict on {Method} {Path}", context.Request.Method, context.Request.Path);
            await WriteProblemAsync(context, StatusCodes.Status409Conflict, "Conflict",
                "This record was changed by someone else. Please reload and try again.", ex);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unhandled exception on {Method} {Path}", context.Request.Method, context.Request.Path);
            await WriteProblemAsync(context, StatusCodes.Status500InternalServerError, "Server error",
                "Something went wrong on our side. Please try again.", ex);
        }
    }

    private async Task WriteProblemAsync(HttpContext context, int status, string title, string message, Exception ex)
    {
        // If the response already started we cannot change it
        if (context.Response.HasStarted) throw ex;

        context.Response.Clear();
        context.Response.StatusCode = status;
        context.Response.ContentType = "application/problem+json";

        var problem = new ProblemDetails
        {
            Status = status,
            Title = title,
            // Technical detail only on a developer machine, never in production
            Detail = environment.IsDevelopment() ? ex.ToString() : null,
        };
        problem.Extensions["traceId"] = context.TraceIdentifier; // same value as the X-Correlation-ID header
        problem.Extensions["message"] = message;                 // Angular reads "message" for error text

        await context.Response.WriteAsJsonAsync(problem, options: null, contentType: "application/problem+json");
    }
}
