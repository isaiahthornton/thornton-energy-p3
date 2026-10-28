using System.Net.Http.Json;
using ThorntonEnergy.Api.Dtos;

namespace ThorntonEnergy.Api.Services;

// The outage numbers the admin dashboard needs, which the Outages service owns ("Services:Outages" in configuration).
// An interface, so tests can use a fake instead of HTTP.
public interface IOutagesClient
{
    Task<OutageSummaryDto> GetSummaryAsync();
    Task<List<AuditLogDto>> GetActivityAsync(int take);
}

// Calls the Outages service's admin-only report endpoints with the admin's own token (see ServiceClients).
// A failed call throws HttpRequestException, which the error middleware turns into 503.
public class OutagesClient(HttpClient http) : IOutagesClient
{
    public async Task<OutageSummaryDto> GetSummaryAsync() =>
        await http.GetFromJsonAsync<OutageSummaryDto>("api/outages/summary")
        ?? throw new HttpRequestException("Outages returned an empty summary.");

    public async Task<List<AuditLogDto>> GetActivityAsync(int take) =>
        await http.GetFromJsonAsync<List<AuditLogDto>>($"api/outages/activity?take={take}") ?? [];
}
