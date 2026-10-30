using Microsoft.EntityFrameworkCore;
using ThorntonEnergy.Customers.Api.Data;
using ThorntonEnergy.Customers.Api.Dtos;
using ThorntonEnergy.Shared.Common;

namespace ThorntonEnergy.Customers.Api.Services;

public interface ICustomerInsightService
{
    Task<Result<CustomerSummaryDto>> GetSummaryAsync();
    Task<Result<List<AuditLogDto>>> GetActivityAsync(int take);
}

// The customer numbers for the admin dashboard (the monolith's /api/admin endpoints combine them with the others).
// It uses the DbContext directly (not IRepository) because a view is not an entity table.
public class CustomerInsightService(CustomersDbContext db) : ICustomerInsightService
{
    public async Task<Result<CustomerSummaryDto>> GetSummaryAsync()
    {
        var summary = await db.CustomerSummary.AsNoTracking().FirstAsync(); // the view always returns exactly one row
        return Result<CustomerSummaryDto>.Success(new CustomerSummaryDto(summary.ActiveCustomers));
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
