using ThorntonEnergy.Api.Models;

namespace ThorntonEnergy.Api.Dtos;

// What the API sends back. Same JSON fields Angular already expects; entities never leave the server directly.
// Admin insight (dashboard numbers and the activity feed)
public record AdminKpisDto(int ActiveCustomers, decimal UnpaidBalance, decimal OverdueBalance, decimal RevenueThisMonth,
    int OpenOutages, decimal AvgKwhLastMonth);

// What the Customers service's GET api/customers/summary returns (CustomersClient reads it)
public record CustomerSummaryDto(int ActiveCustomers);

// What the Billing service's GET api/bills/summary returns (BillingClient reads it)
public record BillingSummaryDto(decimal UnpaidBalance, decimal OverdueBalance, decimal RevenueThisMonth, decimal AvgKwhLastMonth);

// What the Outages service's GET api/outages/summary returns (OutagesClient reads it)
public record OutageSummaryDto(int OpenOutages);

public record MonthlyUsageDto(DateOnly Month, long TotalKwh, decimal TotalBilled);

public record AuditLogDto(int Id, string TableName, int RecordId, string ColumnName, string? OldValue, string? NewValue,
    DateTime ChangedAt, string? ChangedBy);
