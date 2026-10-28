using Microsoft.EntityFrameworkCore;
using ThorntonEnergy.Api.Data;
using ThorntonEnergy.Api.Dtos;
using ThorntonEnergy.Api.Models;
using ThorntonEnergy.Shared.Common;

namespace ThorntonEnergy.Api.Services;

public interface IAdminInsightService
{
    Task<Result<AdminKpisDto>> GetKpisAsync();
    Task<Result<List<MonthlyUsageDto>>> GetUsageTrendAsync(int months);
    Task<Result<List<AuditLogDto>>> GetActivityAsync(int take);
}

// Reads the numbers the admin dashboard shows. The heavy lifting is done in SQL (a view and a stored procedure),
// because grouping and summing is what the database is good at. It uses the DbContext directly (not IRepository)
// because a view and a stored procedure are not entity tables.
public class AdminInsightService(AppDbContext db) : IAdminInsightService
{
    public async Task<Result<AdminKpisDto>> GetKpisAsync()
    {
        var kpis = await db.AdminKpis.AsNoTracking().FirstAsync(); // the view always returns exactly one row
        return Result<AdminKpisDto>.Success(new AdminKpisDto(
            kpis.ActiveCustomers, kpis.UnpaidBalance, kpis.OverdueBalance,
            kpis.RevenueThisMonth, kpis.OpenOutages, kpis.AvgKwhLastMonth));
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
