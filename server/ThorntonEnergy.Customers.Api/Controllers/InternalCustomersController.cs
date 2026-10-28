using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ThorntonEnergy.Customers.Api.Dtos;
using ThorntonEnergy.Customers.Api.Services;
using ThorntonEnergy.Shared.Auth;
using ThorntonEnergy.Shared.Controllers;

namespace ThorntonEnergy.Customers.Api.Controllers;

// Service-to-service only. Login and register happen before anyone is signed in, so there is no user token to pass
// along; the monolith signs a short-lived token with the "Service" role instead. Users (even admins) get 403 here,
// and nginx / the ingress refuse /api/internal from browsers as a second layer.
[ApiController]
[Route("api/internal/customers")]
[Authorize(Roles = AppRoles.Service)]
public class InternalCustomersController(ICustomerAccountService accounts) : AppControllerBase
{
    // GET api/internal/customers/5 (login: is the account active?)
    [HttpGet("{id:int}")]
    public async Task<ActionResult<CustomerAccountDto>> Get(int id) =>
        FromResult(await accounts.GetAsync(id));

    // GET api/internal/customers/lookup?accountNumber=TE-100001&email=jane@example.com (register)
    [HttpGet("lookup")]
    public async Task<ActionResult<CustomerAccountDto>> Find([FromQuery] string accountNumber, [FromQuery] string email) =>
        FromResult(await accounts.FindAsync(accountNumber, email));
}
