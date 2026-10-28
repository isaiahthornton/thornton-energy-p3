using System.ComponentModel.DataAnnotations;
using ThorntonEnergy.Api.Models;

namespace ThorntonEnergy.Api.Dtos;

// What the API accepts. Only the fields a client may choose: totals, timestamps and statuses are set by the server.
// [ApiController] checks these attributes and answers 400 automatically when they fail.
public record CreateBillRequest(
    [Range(1, int.MaxValue)] int CustomerId,
    DateOnly PeriodStart,
    DateOnly PeriodEnd,
    [Range(0, 1_000_000)] int KwhUsed);

public record CreateMeterReadingRequest(
    [Range(1, int.MaxValue)] int MeterId,
    DateOnly ReadingDate,
    [Range(0, int.MaxValue)] int ReadingValue);

public record CreateOutageRequest(
    int? CustomerId, // only used when an admin reports for a customer
    [Required, StringLength(200)] string Address,
    [Required, StringLength(1000)] string Description);

public record RatePlanRequest(
    [Required, StringLength(100)] string Name,
    [Range(0.0001, 100)] decimal PricePerKwh,
    [Range(0, 1000)] decimal MonthlyFee,
    [Required, StringLength(500)] string Description);
