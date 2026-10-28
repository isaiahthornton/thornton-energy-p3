using Microsoft.EntityFrameworkCore;
using ThorntonEnergy.Api.Data;
using ThorntonEnergy.Api.Dtos;
using ThorntonEnergy.Shared.Common;

namespace ThorntonEnergy.Api.Services;

public interface IAdminInsightService
{
    Task<Result<AdminKpisDto>> GetKpisAsync();
    Task<Result<List<MonthlyUsageDto>>> GetUsageTrendAsync(int months);
    Task<Result<List<AuditLogDto>>> GetActivityAsync(int take);
}

// Reads the numbers the admin dashboard shows. Customers, bills and outages each live in their own service, so every
// answer is put together from them ("API composition"). Angular still calls /api/admin/... and gets the same JSON.
public class AdminInsightService(
    AppDbContext db, ICustomersClient customers, IBillingClient billing, IOutagesClient outages) : IAdminInsightService
{
    public async Task<Result<AdminKpisDto>> GetKpisAsync()
    {
        var active = await customers.GetSummaryAsync();
        var bills = await billing.GetSummaryAsync();
        var open = await outages.GetSummaryAsync();

        return Result<AdminKpisDto>.Success(new AdminKpisDto(
            active.ActiveCustomers, bills.UnpaidBalance, bills.OverdueBalance,
            bills.RevenueThisMonth, open.OpenOutages, bills.AvgKwhLastMonth));
    }

    // Usage and billed totals come only from bills, so this is handed straight to the Billing service
    public async Task<Result<List<MonthlyUsageDto>>> GetUsageTrendAsync(int months)
    {
        if (months is < 1 or > 60)
            return Result<List<MonthlyUsageDto>>.Invalid("months must be between 1 and 60.");

        return Result<List<MonthlyUsageDto>>.Success(await billing.GetUsageTrendAsync(months));
    }

    // Customer, bill and outage changes from their services, plus the history recorded here before the split:
    // merged, newest first
    public async Task<Result<List<AuditLogDto>>> GetActivityAsync(int take)
    {
        if (take is < 1 or > 200)
            return Result<List<AuditLogDto>>.Invalid("take must be between 1 and 200.");

        var history = await db.AuditLogs.AsNoTracking()
            .OrderByDescending(a => a.ChangedAt).ThenByDescending(a => a.Id)
            .Take(take)
            .Select(a => new AuditLogDto(a.Id, a.TableName, a.RecordId, a.ColumnName, a.OldValue, a.NewValue, a.ChangedAt, a.ChangedBy))
            .ToListAsync();
        var fromCustomers = await customers.GetActivityAsync(take);
        var fromBilling = await billing.GetActivityAsync(take);
        var fromOutages = await outages.GetActivityAsync(take);

        // Each source already returned its newest "take" rows, so the newest "take" overall are among them.
        // (Ids come from several databases and can repeat, so Angular tracks the rows by position, not id.)
        return Result<List<AuditLogDto>>.Success(history.Concat(fromCustomers).Concat(fromBilling).Concat(fromOutages)
            .OrderByDescending(a => a.ChangedAt)
            .Take(take)
            .ToList());
    }
}
