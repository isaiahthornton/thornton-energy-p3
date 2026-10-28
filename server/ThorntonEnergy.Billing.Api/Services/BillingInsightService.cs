using Microsoft.EntityFrameworkCore;
using ThorntonEnergy.Billing.Api.Data;
using ThorntonEnergy.Billing.Api.Dtos;
using ThorntonEnergy.Billing.Api.Models;
using ThorntonEnergy.Shared.Common;

namespace ThorntonEnergy.Billing.Api.Services;

public interface IBillingInsightService
{
    Task<Result<BillingSummaryDto>> GetSummaryAsync();
    Task<Result<List<MonthlyUsageDto>>> GetUsageTrendAsync(int months);
    Task<Result<List<AuditLogDto>>> GetActivityAsync(int take);
}

// The billing numbers for the admin dashboard (the monolith's /api/admin endpoints combine them with its own).
// The heavy lifting is done in SQL (a view and a stored procedure),
// because grouping and summing is what the database is good at. It uses the DbContext directly (not IRepository)
// because a view and a stored procedure are not entity tables.
public class BillingInsightService(BillingDbContext db) : IBillingInsightService
{
    public async Task<Result<BillingSummaryDto>> GetSummaryAsync()
    {
        var summary = await db.BillingSummary.AsNoTracking().FirstAsync(); // the view always returns exactly one row
        return Result<BillingSummaryDto>.Success(new BillingSummaryDto(
            summary.UnpaidBalance, summary.OverdueBalance, summary.RevenueThisMonth, summary.AvgKwhLastMonth));
    }

    public async Task<Result<List<MonthlyUsageDto>>> GetUsageTrendAsync(int months)
    {
        if (months is < 1 or > 60)
            return Result<List<MonthlyUsageDto>>.Invalid("months must be between 1 and 60.");

        // Calls the stored procedure. The {months} placeholder becomes a SQL parameter, not string concatenation.
        var rows = await db.Database
            .SqlQuery<MonthlyUsageRow>($"EXEC dbo.usp_GetMonthlyUsage @Months = {months}")
            .ToListAsync();

        return Result<List<MonthlyUsageDto>>.Success(
            rows.Select(r => new MonthlyUsageDto(r.Month, r.TotalKwh, r.TotalBilled)).ToList());
    }

    public async Task<Result<List<AuditLogDto>>> GetActivityAsync(int take)
    {
        if (take is < 1 or > 200)
            return Result<List<AuditLogDto>>.Invalid("take must be between 1 and 200.");

        var rows = await db.AuditLogs.AsNoTracking()
            .OrderByDescending(a => a.ChangedAt).ThenByDescending(a => a.Id)
            .Take(take)
            .ToListAsync();

        return Result<List<AuditLogDto>>.Success(rows
            .Select(a => new AuditLogDto(a.Id, a.TableName, a.RecordId, a.ColumnName, a.OldValue, a.NewValue, a.ChangedAt, a.ChangedBy))
            .ToList());
    }
}
