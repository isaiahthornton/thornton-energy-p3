using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ThorntonEnergy.Api.Dtos;
using ThorntonEnergy.Api.Services;
using ThorntonEnergy.Shared.Auth;
using ThorntonEnergy.Shared.Controllers;

namespace ThorntonEnergy.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class MeterReadingsController(IMeterService meters) : AppControllerBase
{
    // GET api/meterReadings (?meterId=) | customers only get readings from their own meters (oldest first, for charts)
    [HttpGet]
    public async Task<ActionResult<List<MeterReadingDto>>> GetAll([FromQuery] int? meterId) =>
        FromResult(await meters.GetReadingsAsync(meterId));

    // GET api/meterReadings/5 (403 if the meter belongs to another customer)
    [HttpGet("{id:int}")]
    public async Task<ActionResult<MeterReadingDto>> GetById(int id) =>
        FromResult(await meters.GetReadingByIdAsync(id));

    // POST api/meterReadings (admin only)
    [Authorize(Roles = AppRoles.Admin)]
    [HttpPost]
    public async Task<ActionResult<MeterReadingDto>> Create(CreateMeterReadingRequest request)
    {
        var result = await meters.CreateReadingAsync(request);
        if (!result.IsSuccess) return Failure(result);
        return CreatedAtAction(nameof(GetById), new { id = result.Value!.Id }, result.Value);
    }
}
