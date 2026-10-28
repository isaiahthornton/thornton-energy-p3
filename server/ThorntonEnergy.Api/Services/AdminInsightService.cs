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

// Reads the numbers the admin dashboard shows. Bills and outages live in their own services, so each answer is put
// together from several places ("API composition"): this database (customers), the Billing service and the Outages
// service. Angular still calls /api/admin/... and gets the same JSON as before.
public class AdminInsightService(AppDbContext db, IBillingClient billing, IOutagesClient outages) : IAdminInsightService
{
    public async Task<Result<AdminKpisDto>> GetKpisAsync()
    {
        var kpis = await db.AdminKpis.AsNoTracking().FirstAsync(); // the view always returns exactly one row
        var bills = await billing.GetSummaryAsync();
        var open = await outages.GetSummaryAsync();

        return Result<AdminKpisDto>.Success(new AdminKpisDto(
            kpis.ActiveCustomers, bills.UnpaidBalance, bills.OverdueBalance,
            bills.RevenueThisMonth, open.OpenOutages, bills.AvgKwhLastMonth));
    }

    // Usage and billed totals come only from bills, so this is handed straight to the Billing service
    public async Task<Result<List<MonthlyUsageDto>>> GetUsageTrendAsync(int months)
    {
        if (months is < 1 or > 60)
            return Result<List<MonthlyUsageDto>>.Invalid("months must be between 1 and 60.");

        return Result<List<MonthlyUsageDto>>.Success(await billing.GetUsageTrendAsync(months));
    }

    // Customer changes from this database, bill changes from Billing, outage changes from Outages: merged, newest first
    public async Task<Result<List<AuditLogDto>>> GetActivityAsync(int take)
    {
        if (take is < 1 or > 200)
            return Result<List<AuditLogDto>>.Invalid("take must be between 1 and 200.");

        var local = await db.AuditLogs.AsNoTracking()
            .OrderByDescending(a => a.ChangedAt).ThenByDescending(a => a.Id)
            .Take(take)
            .Select(a => new AuditLogDto(a.Id, a.TableName, a.RecordId, a.ColumnName, a.OldValue, a.NewValue, a.ChangedAt, a.ChangedBy))
            .ToListAsync();
        var fromBilling = await billing.GetActivityAsync(take);
        var fromOutages = await outages.GetActivityAsync(take);

        // Each source already returned its newest "take" rows, so the newest "take" overall are among them.
        // (Ids come from several databases and can repeat, so Angular tracks the rows by position, not id.)
        return Result<List<AuditLogDto>>.Success(local.Concat(fromBilling).Concat(fromOutages)
            .OrderByDescending(a => a.ChangedAt)
            .Take(take)
            .ToList());
    }
}
