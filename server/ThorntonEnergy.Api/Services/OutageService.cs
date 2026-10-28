using AutoMapper;
using Microsoft.EntityFrameworkCore;
using ThorntonEnergy.Api.Data;
using ThorntonEnergy.Api.Dtos;
using ThorntonEnergy.Api.Models;
using ThorntonEnergy.Shared.Auth;
using ThorntonEnergy.Shared.Common;
using ThorntonEnergy.Shared.Data;

namespace ThorntonEnergy.Api.Services;

public interface IOutageService
{
    Task<Result<List<OutageDto>>> GetAllAsync(int? customerId);
    Task<Result<List<OutageMapDto>>> GetMapAsync();
    Task<Result<OutageDto>> GetByIdAsync(int id);
    Task<Result<OutageDto>> CreateAsync(CreateOutageRequest request);
    Task<Result<OutageDto>> UpdateAsync(int id, OutagePatch patch);
}

public class OutageService(
    IRepository<Outage> outages,
    IRepository<Customer> customers,
    ICurrentUser currentUser,
    IUtilityClock clock,
    IMapper mapper,
    ILogger<OutageService> logger) : IOutageService
{
    // Admin: all outages (or one customer's). Customer: only their own. Newest report first.
    public async Task<Result<List<OutageDto>>> GetAllAsync(int? customerId)
    {
        if (!currentUser.TryGetCustomerScope(customerId, out var scope))
            return Result<List<OutageDto>>.Forbidden();

        var query = outages.Query().AsNoTracking();
        if (scope is not null) query = query.Where(o => o.CustomerId == scope);
        var list = await query.OrderByDescending(o => o.TimeReported).ToListAsync();

        return Result<List<OutageDto>>.Success(mapper.Map<List<OutageDto>>(list));
    }

    // Admin only (the controller enforces the role). Every outage whose customer has map coordinates, newest first.
    public async Task<Result<List<OutageMapDto>>> GetMapAsync()
    {
        var rows = await outages.Query().AsNoTracking()
            .Where(o => o.Customer.Latitude != null && o.Customer.Longitude != null) // EF turns this into a SQL JOIN
            .OrderByDescending(o => o.TimeReported)
            .Select(o => new
            {
                o.Id, o.CustomerId, o.Customer.FirstName, o.Customer.LastName, o.Address, o.Description,
                o.TimeReported, o.TimeResolved, o.Status, o.Customer.Latitude, o.Customer.Longitude,
            })
            .ToListAsync();

        var pins = rows.Select(r => new OutageMapDto(
            r.Id, r.CustomerId, $"{r.FirstName} {r.LastName}", r.Address, r.Description,
            r.TimeReported, r.TimeResolved, r.Status, r.Latitude!.Value, r.Longitude!.Value)).ToList();

        return Result<List<OutageMapDto>>.Success(pins);
    }

    public async Task<Result<OutageDto>> GetByIdAsync(int id)
    {
        var outage = await outages.GetByIdAsync(id);
        if (outage is null) return Result<OutageDto>.NotFound();
        if (!currentUser.CanAccessCustomer(outage.CustomerId)) return Result<OutageDto>.Forbidden();

        return Result<OutageDto>.Success(mapper.Map<OutageDto>(outage));
    }

    // Customers always report for themselves; admins can report for any customer
    public async Task<Result<OutageDto>> CreateAsync(CreateOutageRequest request)
    {
        int customerId;
        if (currentUser.IsAdmin)
        {
            if (request.CustomerId is not int requested) return Result<OutageDto>.Invalid("Choose a customer for this outage.");
            if (await customers.GetByIdAsync(requested) is null) return Result<OutageDto>.Invalid("That customer does not exist.");
            customerId = requested;
        }
        else
        {
            // Ignore whatever account the browser sent
            if (currentUser.CustomerId is not int ownId) return Result<OutageDto>.Forbidden();
            customerId = ownId;
        }

        var outage = mapper.Map<Outage>(request);
        outage.CustomerId = customerId;
        outage.Status = OutageStatus.Reported;   // every report starts as "reported"
        outage.TimeReported = clock.Now;
        outage.TimeResolved = null;
        outages.Add(outage);
        await outages.SaveChangesAsync();

        logger.LogInformation("Outage {OutageId} reported for customer {CustomerId}", outage.Id, customerId);
        return Result<OutageDto>.Success(mapper.Map<OutageDto>(outage));
    }

    // Admin only. Status and TimeResolved never disagree, and the server stamps the resolve time.
    public async Task<Result<OutageDto>> UpdateAsync(int id, OutagePatch patch)
    {
        var outage = await outages.GetByIdAsync(id);
        if (outage is null) return Result<OutageDto>.NotFound();

        logger.LogInformation("Outage {OutageId} status changed {OldStatus} -> {NewStatus}", id, outage.Status, patch.Status);
        outage.Status = patch.Status;
        outage.TimeResolved = patch.Status == OutageStatus.Resolved ? outage.TimeResolved ?? clock.Now : null;

        await outages.SaveChangesAsync();
        return Result<OutageDto>.Success(mapper.Map<OutageDto>(outage));
    }
}
