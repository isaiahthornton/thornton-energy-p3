using AutoMapper;
using Microsoft.EntityFrameworkCore;
using ThorntonEnergy.Customers.Api.Dtos;
using ThorntonEnergy.Customers.Api.Models;
using ThorntonEnergy.Shared.Common;
using ThorntonEnergy.Shared.Data;

namespace ThorntonEnergy.Customers.Api.Services;

public interface IRatePlanService
{
    Task<Result<List<RatePlanDto>>> GetAllAsync();
    Task<Result<RatePlanDto>> GetByIdAsync(int id);
    Task<Result<RatePlanDto>> CreateAsync(RatePlanRequest request);
    Task<Result<RatePlanDto>> ReplaceAsync(int id, RatePlanRequest request);
    Task<Result<bool>> DeleteAsync(int id);
}

public class RatePlanService(
    IRepository<RatePlan> plans,
    IRepository<Customer> customers,
    IMapper mapper,
    ILogger<RatePlanService> logger) : IRatePlanService
{
    public async Task<Result<List<RatePlanDto>>> GetAllAsync()
    {
        var list = await plans.Query().AsNoTracking().OrderBy(r => r.Id).ToListAsync();
        return Result<List<RatePlanDto>>.Success(mapper.Map<List<RatePlanDto>>(list));
    }

    public async Task<Result<RatePlanDto>> GetByIdAsync(int id)
    {
        var plan = await plans.GetByIdAsync(id);
        return plan is null
            ? Result<RatePlanDto>.NotFound()
            : Result<RatePlanDto>.Success(mapper.Map<RatePlanDto>(plan));
    }

    public async Task<Result<RatePlanDto>> CreateAsync(RatePlanRequest request)
    {
        var plan = mapper.Map<RatePlan>(request);
        plans.Add(plan);
        await plans.SaveChangesAsync();

        logger.LogInformation("Rate plan {RatePlanId} created: {Name}", plan.Id, plan.Name);
        return Result<RatePlanDto>.Success(mapper.Map<RatePlanDto>(plan));
    }

    // PUT replaces every field
    public async Task<Result<RatePlanDto>> ReplaceAsync(int id, RatePlanRequest request)
    {
        var plan = await plans.GetByIdAsync(id);
        if (plan is null) return Result<RatePlanDto>.NotFound();

        mapper.Map(request, plan);
        await plans.SaveChangesAsync();

        logger.LogInformation("Rate plan {RatePlanId} updated", id);
        return Result<RatePlanDto>.Success(mapper.Map<RatePlanDto>(plan));
    }

    // Blocked while customers are on the plan
    public async Task<Result<bool>> DeleteAsync(int id)
    {
        var plan = await plans.GetByIdAsync(id);
        if (plan is null) return Result<bool>.NotFound();

        if (await customers.Query().AnyAsync(c => c.RatePlanId == id))
            return Result<bool>.Conflict("Customers are still on this plan. Move them to another plan first.");

        plans.Remove(plan);
        await plans.SaveChangesAsync();

        logger.LogInformation("Rate plan {RatePlanId} deleted", id);
        return Result<bool>.Success(true);
    }
}
