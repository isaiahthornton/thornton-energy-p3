using System.ComponentModel.DataAnnotations;
using ThorntonEnergy.Api.Models;

namespace ThorntonEnergy.Api.Dtos;

// What the API accepts. Only the fields a client may choose: totals, timestamps and statuses are set by the server.
// [ApiController] checks these attributes and answers 400 automatically when they fail.
public record RatePlanRequest(
    [Required, StringLength(100)] string Name,
    [Range(0.0001, 100)] decimal PricePerKwh,
    [Range(0, 1000)] decimal MonthlyFee,
    [Required, StringLength(500)] string Description);
