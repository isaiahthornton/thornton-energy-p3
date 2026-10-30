using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ThorntonEnergy.Customers.Api.Dtos;
using ThorntonEnergy.Customers.Api.Services;
using ThorntonEnergy.Shared.Auth;
using ThorntonEnergy.Shared.Controllers;

namespace ThorntonEnergy.Customers.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class CustomersController(ICustomerService customers) : AppControllerBase
{
    // GET api/customers (admin only, sorted by last name)
    [Authorize(Roles = AppRoles.Admin)]
    [HttpGet]
    public async Task<ActionResult<List<CustomerDto>>> GetAll() =>
        FromResult(await customers.GetAllAsync());

    // GET api/customers/1 (customers: only their own record)
    [HttpGet("{id:int}")]
    public async Task<ActionResult<CustomerDto>> GetById(int id) =>
        FromResult(await customers.GetByIdAsync(id));

    // PATCH api/customers/1   body: { "ratePlanId": 2 }  or  { "status": "suspended" }
    [HttpPatch("{id:int}")]
    public async Task<ActionResult<CustomerDto>> Update(int id, CustomerPatch patch) =>
        FromResult(await customers.UpdateAsync(id, patch));
}
