using Microsoft.EntityFrameworkCore;
using ThorntonEnergy.Customers.Api.Dtos;
using ThorntonEnergy.Customers.Api.Models;
using ThorntonEnergy.Shared.Common;
using ThorntonEnergy.Shared.Data;

namespace ThorntonEnergy.Customers.Api.Services;

// What the monolith's login and register need to know about an account, before anyone is signed in.
// Only reachable with a service token (see InternalCustomersController), so no ownership rules apply here.
public interface ICustomerAccountService
{
    Task<Result<CustomerAccountDto>> GetAsync(int id);
    Task<Result<CustomerAccountDto>> FindAsync(string accountNumber, string email);
}

public class CustomerAccountService(IRepository<Customer> customers) : ICustomerAccountService
{
    // Login: is this customer's account still active?
    public async Task<Result<CustomerAccountDto>> GetAsync(int id)
    {
        var customer = await customers.GetByIdAsync(id);
        return customer is null
            ? Result<CustomerAccountDto>.NotFound()
            : Result<CustomerAccountDto>.Success(new CustomerAccountDto(customer.Id, customer.Status));
    }

    // Register: the account number and email must both match the same customer
    public async Task<Result<CustomerAccountDto>> FindAsync(string accountNumber, string email)
    {
        var customer = await customers.Query().AsNoTracking()
            .FirstOrDefaultAsync(c => c.AccountNumber == accountNumber && c.Email == email);
        return customer is null
            ? Result<CustomerAccountDto>.NotFound()
            : Result<CustomerAccountDto>.Success(new CustomerAccountDto(customer.Id, customer.Status));
    }
}
