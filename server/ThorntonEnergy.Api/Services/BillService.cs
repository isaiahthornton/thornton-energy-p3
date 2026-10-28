using AutoMapper;
using Microsoft.EntityFrameworkCore;
using ThorntonEnergy.Api.Data;
using ThorntonEnergy.Api.Dtos;
using ThorntonEnergy.Api.Models;
using ThorntonEnergy.Shared.Auth;
using ThorntonEnergy.Shared.Common;
using ThorntonEnergy.Shared.Data;

namespace ThorntonEnergy.Api.Services;

public interface IBillService
{
    Task<Result<List<BillDto>>> GetAllAsync(int? customerId);
    Task<Result<BillDto>> GetByIdAsync(int id);
    Task<Result<BillDto>> CreateAsync(CreateBillRequest request);
    Task<Result<BillDto>> UpdateAsync(int id, BillPatch patch);
}

public class BillService(
    IRepository<Bill> bills,
    IRepository<Customer> customers,
    ICurrentUser currentUser,
    IUtilityClock clock,
    IMapper mapper,
    ILogger<BillService> logger) : IBillService
{
    // Admin: all bills (or one customer's). Customer: only their own. Newest period first.
    public async Task<Result<List<BillDto>>> GetAllAsync(int? customerId)
    {
        if (!currentUser.TryGetCustomerScope(customerId, out var scope))
            return Result<List<BillDto>>.Forbidden();

        var query = bills.Query().AsNoTracking();
        if (scope is not null) query = query.Where(b => b.CustomerId == scope);
        var list = await query.OrderByDescending(b => b.PeriodStart).ToListAsync();

        return Result<List<BillDto>>.Success(list.Select(ToDto).ToList());
    }

    public async Task<Result<BillDto>> GetByIdAsync(int id)
    {
        var bill = await bills.GetByIdAsync(id);
        if (bill is null) return Result<BillDto>.NotFound();
        if (!currentUser.CanAccessCustomer(bill.CustomerId)) return Result<BillDto>.Forbidden();

        return Result<BillDto>.Success(ToDto(bill));
    }

    // Admin only. The browser sends usage; the server prices it from the customer's rate plan.
    public async Task<Result<BillDto>> CreateAsync(CreateBillRequest request)
    {
        if (request.PeriodEnd < request.PeriodStart)
            return Result<BillDto>.Invalid("The billing period must end on or after it starts.");

        var customer = await customers.Query()
            .Include(c => c.RatePlan)
            .FirstOrDefaultAsync(c => c.Id == request.CustomerId);
        if (customer is null) return Result<BillDto>.Invalid("That customer does not exist.");

        // One bill per customer per billing period
        if (await bills.Query().AnyAsync(b => b.CustomerId == request.CustomerId && b.PeriodStart == request.PeriodStart))
            return Result<BillDto>.Conflict("That customer already has a bill for this billing period.");

        var (energy, service, total) = BillingRules.Calculate(request.KwhUsed, customer.RatePlan);

        var bill = mapper.Map<Bill>(request);
        bill.EnergyCharge = energy;
        bill.ServiceCharge = service;
        bill.AmountDue = total;
        bill.DueDate = BillingRules.DueDate(request.PeriodEnd);
        bill.Status = BillStatus.Unpaid;
        bills.Add(bill);
        await bills.SaveChangesAsync();

        logger.LogInformation("Bill {BillId} created for customer {CustomerId}: {Kwh} kWh, total {Total}",
            bill.Id, bill.CustomerId, bill.KwhUsed, bill.AmountDue);
        return Result<BillDto>.Success(ToDto(bill));
    }

    // PATCH. A customer may only pay their own bill (once); an admin may also change the status.
    public async Task<Result<BillDto>> UpdateAsync(int id, BillPatch patch)
    {
        var bill = await bills.GetByIdAsync(id);
        if (bill is null) return Result<BillDto>.NotFound();
        if (!currentUser.CanAccessCustomer(bill.CustomerId)) return Result<BillDto>.Forbidden();

        if (!currentUser.IsAdmin && patch.Status != BillStatus.Paid) return Result<BillDto>.Forbidden();

        if (patch.Status == BillStatus.Overdue)
            return Result<BillDto>.Invalid("Overdue is worked out from the due date; it cannot be set by hand.");

        if (patch.Status == BillStatus.Paid)
        {
            if (bill.Status == BillStatus.Paid) return Result<BillDto>.Conflict("This bill has already been paid.");

            bill.Status = BillStatus.Paid;
            // The payment date is always the server's today, never the browser's
            bill.PaidDate = clock.Today;
            logger.LogInformation("Bill {BillId} paid for customer {CustomerId}, amount {Amount}",
                bill.Id, bill.CustomerId, bill.AmountDue);
        }
        else if (patch.Status == BillStatus.Unpaid)
        {
            // Admin reopening a bill
            bill.Status = BillStatus.Unpaid;
            bill.PaidDate = null;
            logger.LogInformation("Bill {BillId} reopened by admin", bill.Id);
        }

        await bills.SaveChangesAsync();
        return Result<BillDto>.Success(ToDto(bill));
    }

    // Entity -> DTO, with "overdue" calculated from today's date
    private BillDto ToDto(Bill bill) =>
        mapper.Map<BillDto>(bill) with { Status = BillingRules.EffectiveStatus(bill, clock.Today) };
}
