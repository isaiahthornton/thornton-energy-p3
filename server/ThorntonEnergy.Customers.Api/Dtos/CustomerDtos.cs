using System.ComponentModel.DataAnnotations;
using ThorntonEnergy.Customers.Api.Models;

namespace ThorntonEnergy.Customers.Api.Dtos;

// PATCH body: customers may change their rate plan; only admins may change the status
public record CustomerPatch(int? RatePlanId, AccountStatus? Status);

// POST / PUT body for a rate plan (admin only)
public record RatePlanRequest(
    [Required, StringLength(100)] string Name,
    [Range(0.0001, 100)] decimal PricePerKwh,
    [Range(0, 1000)] decimal MonthlyFee,
    [Required, StringLength(500)] string Description);

// What goes out (same JSON fields Angular already expects)
public record CustomerDto(int Id, string AccountNumber, string FirstName, string LastName, string Phone, string Email,
    string ServiceAddress, int RatePlanId, AccountStatus Status, decimal? Latitude, decimal? Longitude);

public record RatePlanDto(int Id, string Name, decimal PricePerKwh, decimal MonthlyFee, string Description);

// The customer numbers for the admin dashboard (the monolith adds the bill and outage numbers)
public record CustomerSummaryDto(int ActiveCustomers);

public record AuditLogDto(int Id, string TableName, int RecordId, string ColumnName, string? OldValue, string? NewValue,
    DateTime ChangedAt, string? ChangedBy);

// Internal only (service token): what the monolith's login and register need to know about an account
public record CustomerAccountDto(int Id, AccountStatus Status);
