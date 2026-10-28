using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ThorntonEnergy.Shared.Extensions;

// Calling another Thornton Energy service over HTTP.
public static class ServiceClients
{
    // Registers a typed HttpClient for another service. Its address comes from "Services:<name>" in configuration
    // (appsettings.json for dotnet run, Services__<name> in docker-compose / Kubernetes).
    // The caller's own JWT is passed along, so the other service applies the same user's roles and ownership rules.
    public static IHttpClientBuilder AddServiceClient<TClient, TImplementation>(this WebApplicationBuilder builder, string serviceName)
        where TClient : class
        where TImplementation : class, TClient
    {
        var baseUrl = builder.Configuration[$"Services:{serviceName}"]
            ?? throw new InvalidOperationException($"Services:{serviceName} is missing. Set it in appsettings.json or Services__{serviceName}.");

        builder.Services.AddHttpContextAccessor();
        builder.Services.AddTransient<ForwardAuthorizationHandler>();

        return builder.Services
            .AddHttpClient<TClient, TImplementation>(client =>
            {
                client.BaseAddress = new Uri(baseUrl.TrimEnd('/') + "/"); // trailing slash, so "api/..." is appended, not replaced
                client.Timeout = TimeSpan.FromSeconds(30);                  // a paused Azure SQL behind the other service can take a while
            })
            .AddHttpMessageHandler<ForwardAuthorizationHandler>();
    }
}

// Copies "Authorization: Bearer <token>" from the incoming request onto the outgoing one
public class ForwardAuthorizationHandler(IHttpContextAccessor accessor) : DelegatingHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var authorization = accessor.HttpContext?.Request.Headers.Authorization.ToString();
        if (!string.IsNullOrEmpty(authorization))
            request.Headers.TryAddWithoutValidation("Authorization", authorization);

        return base.SendAsync(request, cancellationToken);
    }
}
