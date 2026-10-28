using System.ComponentModel.DataAnnotations;
using ThorntonEnergy.Outages.Api.Models;

namespace ThorntonEnergy.Outages.Api.Dtos;

// POST body. Customers always report for themselves; CustomerId is only used when an admin reports for a customer.
public record CreateOutageRequest(
    int? CustomerId,
    [Required, StringLength(200)] string Address,
    [Required, StringLength(1000)] string Description);

// PATCH body (admin only). The server stamps TimeResolved itself.
public record OutagePatch(OutageStatus Status, DateTime? TimeResolved);

// What goes out (same JSON fields Angular already expects)
public record OutageDto(int Id, int CustomerId, string Address, string Description, DateTime TimeReported,
    DateTime? TimeResolved, OutageStatus Status);

// One pin on the admin outage map: the outage plus its customer's name and location
public record OutageMapDto(int OutageId, int CustomerId, string CustomerName, string Address, string Description,
    DateTime TimeReported, DateTime? TimeResolved, OutageStatus Status, decimal Latitude, decimal Longitude);

// The outage numbers for the admin dashboard (the monolith adds the rest)
public record OutageSummaryDto(int OpenOutages);

public record AuditLogDto(int Id, string TableName, int RecordId, string ColumnName, string? OldValue, string? NewValue,
    DateTime ChangedAt, string? ChangedBy);
