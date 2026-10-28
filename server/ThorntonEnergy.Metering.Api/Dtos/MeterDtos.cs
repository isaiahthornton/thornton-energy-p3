using System.ComponentModel.DataAnnotations;
using ThorntonEnergy.Metering.Api.Models;

namespace ThorntonEnergy.Metering.Api.Dtos;

// POST body (admin only). The service checks it is later and not lower than the previous reading.
public record CreateMeterReadingRequest(
    [Range(1, int.MaxValue)] int MeterId,
    DateOnly ReadingDate,
    [Range(0, int.MaxValue)] int ReadingValue);

// PATCH body: only the field Angular actually changes
public record MeterPatch(MeterStatus Status);

// What goes out (same JSON fields Angular already expects)
public record MeterDto(int Id, int CustomerId, string MeterNumber, DateOnly InstallationDate, MeterStatus Status);

public record MeterReadingDto(int Id, int MeterId, DateOnly ReadingDate, int ReadingValue);
