using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ThorntonEnergy.Customers.Api.Dtos;
using ThorntonEnergy.Customers.Api.Services;
using ThorntonEnergy.Shared.Auth;
using ThorntonEnergy.Shared.Controllers;

namespace ThorntonEnergy.Customers.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = AppRoles.Admin)] // managing plans is admin-only...
public class RatePlansController(IRatePlanService ratePlans) : AppControllerBase
{
    // GET api/ratePlans (...but anyone can view them: the landing page shows the plans)
    [AllowAnonymous]
    [HttpGet]
    public async Task<ActionResult<List<RatePlanDto>>> GetAll() =>
        FromResult(await ratePlans.GetAllAsync());

    // GET api/ratePlans/1
    [HttpGet("{id:int}")]
    public async Task<ActionResult<RatePlanDto>> GetById(int id) =>
        FromResult(await ratePlans.GetByIdAsync(id));

    // POST api/ratePlans
    [HttpPost]
    public async Task<ActionResult<RatePlanDto>> Create(RatePlanRequest request)
    {
        var result = await ratePlans.CreateAsync(request);
        if (!result.IsSuccess) return Failure(result);
        return CreatedAtAction(nameof(GetById), new { id = result.Value!.Id }, result.Value);
    }

    // PUT api/ratePlans/1 (replaces every field)
    [HttpPut("{id:int}")]
    public async Task<ActionResult<RatePlanDto>> Replace(int id, RatePlanRequest request) =>
        FromResult(await ratePlans.ReplaceAsync(id, request));

    // DELETE api/ratePlans/1 (blocked while customers are on the plan)
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var result = await ratePlans.DeleteAsync(id);
        return result.IsSuccess ? NoContent() : Failure(result);
    }
}
