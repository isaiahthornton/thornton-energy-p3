using AutoMapper;
using Microsoft.EntityFrameworkCore;
using ThorntonEnergy.Api.Data;
using ThorntonEnergy.Api.Dtos;
using ThorntonEnergy.Api.Models;
using ThorntonEnergy.Shared.Auth;
using ThorntonEnergy.Shared.Common;
using ThorntonEnergy.Shared.Data;

namespace ThorntonEnergy.Api.Services;

public interface ICustomerService
{
    Task<Result<List<CustomerDto>>> GetAllAsync();
    Task<Result<CustomerDto>> GetByIdAsync(int id);
    Task<Result<CustomerDto>> UpdateAsync(int id, CustomerPatch patch);
}

public class CustomerService(
    IRepository<Customer> customers,
    IRepository<RatePlan> ratePlans,
    ICurrentUser currentUser,
    IMapper mapper,
    ILogger<CustomerService> logger) : ICustomerService
{
    // Admin only (the controller enforces the role). Sorted by last name.
    public async Task<Result<List<CustomerDto>>> GetAllAsync()
    {
        var list = await customers.Query().AsNoTracking().OrderBy(c => c.LastName).ToListAsync();
        return Result<List<CustomerDto>>.Success(mapper.Map<List<CustomerDto>>(list));
    }

    // Customers: only their own record
    public async Task<Result<CustomerDto>> GetByIdAsync(int id)
    {
        if (!currentUser.CanAccessCustomer(id)) return Result<CustomerDto>.Forbidden();

        var customer = await customers.GetByIdAsync(id);
        return customer is null
            ? Result<CustomerDto>.NotFound()
            : Result<CustomerDto>.Success(mapper.Map<CustomerDto>(customer));
    }

    public async Task<Result<CustomerDto>> UpdateAsync(int id, CustomerPatch patch)
    {
        if (!currentUser.CanAccessCustomer(id)) return Result<CustomerDto>.Forbidden();

        // Customers can switch their own rate plan, but only an admin can change an account's status
        if (!currentUser.IsAdmin && patch.Status is not null) return Result<CustomerDto>.Forbidden();

        var customer = await customers.GetByIdAsync(id);
        if (customer is null) return Result<CustomerDto>.NotFound();

        if (patch.RatePlanId is not null)
        {
            if (await ratePlans.GetByIdAsync(patch.RatePlanId.Value) is null)
                return Result<CustomerDto>.Invalid("That rate plan does not exist.");

            logger.LogInformation("Customer {CustomerId} rate plan changed {OldPlan} -> {NewPlan}",
                id, customer.RatePlanId, patch.RatePlanId);
            customer.RatePlanId = patch.RatePlanId.Value;
        }
        if (patch.Status is not null)
        {
            logger.LogInformation("Customer {CustomerId} status changed {OldStatus} -> {NewStatus}",
                id, customer.Status, patch.Status);
            customer.Status = patch.Status.Value;
        }

        await customers.SaveChangesAsync();
        return Result<CustomerDto>.Success(mapper.Map<CustomerDto>(customer));
    }
}
