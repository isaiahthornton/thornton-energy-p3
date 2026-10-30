using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ThorntonEnergy.Metering.Api.Dtos;
using ThorntonEnergy.Metering.Api.Services;
using ThorntonEnergy.Shared.Auth;
using ThorntonEnergy.Shared.Controllers;

namespace ThorntonEnergy.Metering.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class MetersController(IMeterService meters) : AppControllerBase
{
    // GET api/meters (admin: all, or ?customerId=) | customer: only their own (newest install first)
    [HttpGet]
    public async Task<ActionResult<List<MeterDto>>> GetAll([FromQuery] int? customerId) =>
        FromResult(await meters.GetMetersAsync(customerId));

    // PATCH api/meters/1   body: { "status": "inactive" } (admin only)
    [Authorize(Roles = AppRoles.Admin)]
    [HttpPatch("{id:int}")]
    public async Task<ActionResult<MeterDto>> Update(int id, MeterPatch patch) =>
        FromResult(await meters.UpdateMeterAsync(id, patch));
}
