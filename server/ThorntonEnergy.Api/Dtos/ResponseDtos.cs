using ThorntonEnergy.Api.Models;

namespace ThorntonEnergy.Api.Dtos;

// What the API sends back. Same JSON fields Angular already expects; entities never leave the server directly.
public record CustomerDto(int Id, string AccountNumber, string FirstName, string LastName, string Phone, string Email,
    string ServiceAddress, int RatePlanId, AccountStatus Status, decimal? Latitude, decimal? Longitude);

public record RatePlanDto(int Id, string Name, decimal PricePerKwh, decimal MonthlyFee, string Description);

// Admin insight (dashboard numbers and the activity feed)
public record AdminKpisDto(int ActiveCustomers, decimal UnpaidBalance, decimal OverdueBalance, decimal RevenueThisMonth,
    int OpenOutages, decimal AvgKwhLastMonth);

// What the Billing service's GET api/bills/summary returns (BillingClient reads it)
public record BillingSummaryDto(decimal UnpaidBalance, decimal OverdueBalance, decimal RevenueThisMonth, decimal AvgKwhLastMonth);

// What the Outages service's GET api/outages/summary returns (OutagesClient reads it)
public record OutageSummaryDto(int OpenOutages);

public record MonthlyUsageDto(DateOnly Month, long TotalKwh, decimal TotalBilled);

public record AuditLogDto(int Id, string TableName, int RecordId, string ColumnName, string? OldValue, string? NewValue,
    DateTime ChangedAt, string? ChangedBy);
