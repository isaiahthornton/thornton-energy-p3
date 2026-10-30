using ThorntonEnergy.Api.Dtos;
using ThorntonEnergy.Api.Services;

namespace ThorntonEnergy.Api.Tests.Helpers;

// Stands in for the Billing service, so tests never make HTTP calls. Set Activity to control the bill changes it returns.
public class FakeBillingClient : IBillingClient
{
    public List<AuditLogDto> Activity { get; set; } = [];

    public Task<BillingSummaryDto> GetSummaryAsync() =>
        Task.FromResult(new BillingSummaryDto(UnpaidBalance: 40m, OverdueBalance: 20m, RevenueThisMonth: 0m, AvgKwhLastMonth: 100m));

    public Task<List<MonthlyUsageDto>> GetUsageTrendAsync(int months) => Task.FromResult(new List<MonthlyUsageDto>());

    // Like the real service: newest first, at most "take" rows
    public Task<List<AuditLogDto>> GetActivityAsync(int take) =>
        Task.FromResult(Activity.OrderByDescending(a => a.ChangedAt).Take(take).ToList());
}
