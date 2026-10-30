using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ThorntonEnergy.Outages.Api.Dtos;
using ThorntonEnergy.Outages.Api.Services;
using ThorntonEnergy.Shared.Auth;
using ThorntonEnergy.Shared.Controllers;

namespace ThorntonEnergy.Outages.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class OutagesController(IOutageService outages) : AppControllerBase
{
    // GET api/outages (admin: all, or ?customerId=) | customer: only their own (newest report first)
    [HttpGet]
    public async Task<ActionResult<List<OutageDto>>> GetAll([FromQuery] int? customerId) =>
        FromResult(await outages.GetAllAsync(customerId));

    // GET api/outages/map (admin only): every outage with its customer name and location, for the map
    [Authorize(Roles = AppRoles.Admin)]
    [HttpGet("map")]
    public async Task<ActionResult<List<OutageMapDto>>> GetMap() =>
        FromResult(await outages.GetMapAsync());

    // GET api/outages/5 (403 if it belongs to another customer)
    [HttpGet("{id:int}")]
    public async Task<ActionResult<OutageDto>> GetById(int id) =>
        FromResult(await outages.GetByIdAsync(id));

    // POST api/outages (customers report on their own account; admins can report for anyone)
    [HttpPost]
    public async Task<ActionResult<OutageDto>> Create(CreateOutageRequest request)
    {
        var result = await outages.CreateAsync(request);
        if (!result.IsSuccess) return Failure(result);
        return CreatedAtAction(nameof(GetById), new { id = result.Value!.Id }, result.Value);
    }

    // PATCH api/outages/5   body: { "status": "resolved" } (admin only)
    [Authorize(Roles = AppRoles.Admin)]
    [HttpPatch("{id:int}")]
    public async Task<ActionResult<OutageDto>> Update(int id, OutagePatch patch) =>
        FromResult(await outages.UpdateAsync(id, patch));
}
