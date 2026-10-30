using AutoMapper;
using Microsoft.EntityFrameworkCore;
using ThorntonEnergy.Metering.Api.Dtos;
using ThorntonEnergy.Metering.Api.Models;
using ThorntonEnergy.Shared.Auth;
using ThorntonEnergy.Shared.Common;
using ThorntonEnergy.Shared.Data;

namespace ThorntonEnergy.Metering.Api.Services;

public interface IMeterService
{
    Task<Result<List<MeterDto>>> GetMetersAsync(int? customerId);
    Task<Result<MeterDto>> UpdateMeterAsync(int id, MeterPatch patch);
    Task<Result<List<MeterReadingDto>>> GetReadingsAsync(int? meterId);
    Task<Result<MeterReadingDto>> GetReadingByIdAsync(int id);
    Task<Result<MeterReadingDto>> CreateReadingAsync(CreateMeterReadingRequest request);
}

public class MeterService(
    IRepository<Meter> meters,
    IRepository<MeterReading> readings,
    ICurrentUser currentUser,
    IMapper mapper,
    ILogger<MeterService> logger) : IMeterService
{
    // Admin: all meters (or one customer's). Customer: only their own.
    public async Task<Result<List<MeterDto>>> GetMetersAsync(int? customerId)
    {
        if (!currentUser.TryGetCustomerScope(customerId, out var scope))
            return Result<List<MeterDto>>.Forbidden();

        var query = meters.Query().AsNoTracking();
        var list = scope is null
            ? await query.OrderBy(m => m.Id).ToListAsync()
            : await query.Where(m => m.CustomerId == scope).OrderByDescending(m => m.InstallationDate).ToListAsync();

        return Result<List<MeterDto>>.Success(mapper.Map<List<MeterDto>>(list));
    }

    // Admin only
    public async Task<Result<MeterDto>> UpdateMeterAsync(int id, MeterPatch patch)
    {
        var meter = await meters.GetByIdAsync(id);
        if (meter is null) return Result<MeterDto>.NotFound();

        logger.LogInformation("Meter {MeterId} status changed {OldStatus} -> {NewStatus}", id, meter.Status, patch.Status);
        meter.Status = patch.Status;
        await meters.SaveChangesAsync();
        return Result<MeterDto>.Success(mapper.Map<MeterDto>(meter));
    }

    // Customers only get readings from their own meters. Oldest first, for charts.
    public async Task<Result<List<MeterReadingDto>>> GetReadingsAsync(int? meterId)
    {
        if (!currentUser.TryGetCustomerScope(null, out var scope))
            return Result<List<MeterReadingDto>>.Forbidden();

        var query = readings.Query().AsNoTracking();
        if (scope is not null) query = query.Where(r => r.Meter.CustomerId == scope); // EF turns this into a SQL JOIN
        if (meterId is not null) query = query.Where(r => r.MeterId == meterId);
        var list = await query.OrderBy(r => r.ReadingDate).ToListAsync();

        return Result<List<MeterReadingDto>>.Success(mapper.Map<List<MeterReadingDto>>(list));
    }

    public async Task<Result<MeterReadingDto>> GetReadingByIdAsync(int id)
    {
        var reading = await readings.Query().Include(r => r.Meter).FirstOrDefaultAsync(r => r.Id == id);
        if (reading is null) return Result<MeterReadingDto>.NotFound();
        if (!currentUser.CanAccessCustomer(reading.Meter.CustomerId)) return Result<MeterReadingDto>.Forbidden();

        return Result<MeterReadingDto>.Success(mapper.Map<MeterReadingDto>(reading));
    }

    // Admin only. A meter only counts up, so a new reading must be later and not lower than the last one.
    public async Task<Result<MeterReadingDto>> CreateReadingAsync(CreateMeterReadingRequest request)
    {
        if (await meters.GetByIdAsync(request.MeterId) is null)
            return Result<MeterReadingDto>.Invalid("That meter does not exist.");

        var previous = await readings.Query()
            .Where(r => r.MeterId == request.MeterId)
            .OrderByDescending(r => r.ReadingDate)
            .FirstOrDefaultAsync();

        if (previous is not null)
        {
            if (request.ReadingDate <= previous.ReadingDate)
                return Result<MeterReadingDto>.Invalid(
                    $"The reading date must be after the previous reading ({previous.ReadingDate:yyyy-MM-dd}).");
            if (request.ReadingValue < previous.ReadingValue)
                return Result<MeterReadingDto>.Invalid(
                    $"The reading cannot be lower than the previous reading ({previous.ReadingValue}).");
        }

        var reading = mapper.Map<MeterReading>(request);
        readings.Add(reading);
        await readings.SaveChangesAsync();

        logger.LogInformation("Meter {MeterId} reading {ReadingId} recorded: {Value}",
            reading.MeterId, reading.Id, reading.ReadingValue);
        return Result<MeterReadingDto>.Success(mapper.Map<MeterReadingDto>(reading));
    }
}
