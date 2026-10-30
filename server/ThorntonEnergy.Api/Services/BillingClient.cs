using System.Net.Http.Json;
using ThorntonEnergy.Api.Dtos;

namespace ThorntonEnergy.Api.Services;

// The billing numbers the admin dashboard needs, which the Billing service owns ("Services:Billing" in configuration).
// An interface, so tests can use a fake instead of HTTP.
public interface IBillingClient
{
    Task<BillingSummaryDto> GetSummaryAsync();
    Task<List<MonthlyUsageDto>> GetUsageTrendAsync(int months);
    Task<List<AuditLogDto>> GetActivityAsync(int take);
}

// Calls the Billing service's admin-only report endpoints with the admin's own token (see ServiceClients).
// A failed call throws HttpRequestException, which the error middleware turns into 503.
public class BillingClient(HttpClient http) : IBillingClient
{
    public async Task<BillingSummaryDto> GetSummaryAsync() =>
        await http.GetFromJsonAsync<BillingSummaryDto>("api/bills/summary")
        ?? throw new HttpRequestException("Billing returned an empty summary.");

    public async Task<List<MonthlyUsageDto>> GetUsageTrendAsync(int months) =>
        await http.GetFromJsonAsync<List<MonthlyUsageDto>>($"api/bills/usage-trend?months={months}") ?? [];

    public async Task<List<AuditLogDto>> GetActivityAsync(int take) =>
        await http.GetFromJsonAsync<List<AuditLogDto>>($"api/bills/activity?take={take}") ?? [];
}
