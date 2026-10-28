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
public class BillsController(IBillService bills) : AppControllerBase
{
    // GET api/bills (admin: all, or ?customerId=) | customer: only their own (newest period first)
    [HttpGet]
    public async Task<ActionResult<List<BillDto>>> GetAll([FromQuery] int? customerId) =>
        FromResult(await bills.GetAllAsync(customerId));

    // GET api/bills/5 (403 if it belongs to another customer)
    [HttpGet("{id:int}")]
    public async Task<ActionResult<BillDto>> GetById(int id) =>
        FromResult(await bills.GetByIdAsync(id));

    // POST api/bills (admin only: customers cannot create their own bills)
    [Authorize(Roles = AppRoles.Admin)]
    [HttpPost]
    public async Task<ActionResult<BillDto>> Create(CreateBillRequest request)
    {
        var result = await bills.CreateAsync(request);
        if (!result.IsSuccess) return Failure(result);
        return CreatedAtAction(nameof(GetById), new { id = result.Value!.Id }, result.Value);
    }

    // PATCH api/bills/5   body: { "status": "paid" }
    [HttpPatch("{id:int}")]
    public async Task<ActionResult<BillDto>> Update(int id, BillPatch patch) =>
        FromResult(await bills.UpdateAsync(id, patch));
}
