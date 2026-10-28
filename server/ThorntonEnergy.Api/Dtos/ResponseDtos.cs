using ThorntonEnergy.Api.Models;

namespace ThorntonEnergy.Api.Dtos;

// What the API sends back. Same JSON fields Angular already expects; entities never leave the server directly.
public record BillDto(int Id, int CustomerId, DateOnly PeriodStart, DateOnly PeriodEnd, DateOnly DueDate, int KwhUsed,
    decimal AmountDue, BillStatus Status, decimal EnergyCharge, decimal ServiceCharge, DateOnly? PaidDate);

public record CustomerDto(int Id, string AccountNumber, string FirstName, string LastName, string Phone, string Email,
    string ServiceAddress, int RatePlanId, AccountStatus Status, decimal? Latitude, decimal? Longitude);

public record MeterDto(int Id, int CustomerId, string MeterNumber, DateOnly InstallationDate, MeterStatus Status);

public record MeterReadingDto(int Id, int MeterId, DateOnly ReadingDate, int ReadingValue);

public record OutageDto(int Id, int CustomerId, string Address, string Description, DateTime TimeReported,
    DateTime? TimeResolved, OutageStatus Status);

public record RatePlanDto(int Id, string Name, decimal PricePerKwh, decimal MonthlyFee, string Description);

// Admin insight (dashboard numbers and the activity feed)
public record AdminKpisDto(int ActiveCustomers, decimal UnpaidBalance, decimal OverdueBalance, decimal RevenueThisMonth,
    int OpenOutages, decimal AvgKwhLastMonth);

public record MonthlyUsageDto(DateOnly Month, long TotalKwh, decimal TotalBilled);

public record AuditLogDto(int Id, string TableName, int RecordId, string ColumnName, string? OldValue, string? NewValue,
    DateTime ChangedAt, string? ChangedBy);

// One pin on the admin outage map: the outage plus where the customer is
public record OutageMapDto(int OutageId, int CustomerId, string CustomerName, string Address, string Description,
    DateTime TimeReported, DateTime? TimeResolved, OutageStatus Status, decimal Latitude, decimal Longitude);
