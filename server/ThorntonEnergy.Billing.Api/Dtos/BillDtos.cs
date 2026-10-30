using System.ComponentModel.DataAnnotations;
using ThorntonEnergy.Billing.Api.Models;

namespace ThorntonEnergy.Billing.Api.Dtos;

// POST body (admin only). The browser sends usage; the server prices it from the customer's rate plan.
public record CreateBillRequest(
    [Range(1, int.MaxValue)] int CustomerId,
    DateOnly PeriodStart,
    DateOnly PeriodEnd,
    [Range(0, 1_000_000)] int KwhUsed);

// PATCH body: only the fields Angular actually changes
public record BillPatch(BillStatus? Status, DateOnly? PaidDate);

// What goes out (same JSON fields Angular already expects)
public record BillDto(int Id, int CustomerId, DateOnly PeriodStart, DateOnly PeriodEnd, DateOnly DueDate, int KwhUsed,
    decimal AmountDue, BillStatus Status, decimal EnergyCharge, decimal ServiceCharge, DateOnly? PaidDate);

// The billing numbers for the admin dashboard (the monolith adds customer and outage counts)
public record BillingSummaryDto(decimal UnpaidBalance, decimal OverdueBalance, decimal RevenueThisMonth, decimal AvgKwhLastMonth);

public record MonthlyUsageDto(DateOnly Month, long TotalKwh, decimal TotalBilled);

public record AuditLogDto(int Id, string TableName, int RecordId, string ColumnName, string? OldValue, string? NewValue,
    DateTime ChangedAt, string? ChangedBy);
