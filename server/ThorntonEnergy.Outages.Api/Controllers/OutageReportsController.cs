using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ThorntonEnergy.Outages.Api.Dtos;
using ThorntonEnergy.Outages.Api.Services;
using ThorntonEnergy.Shared.Auth;
using ThorntonEnergy.Shared.Controllers;

namespace ThorntonEnergy.Outages.Api.Controllers;

// The outage numbers for the admin dashboard. The monolith's /api/admin endpoints call these and combine them
// with the customer and bill numbers, so Angular keeps calling /api/admin as before. Admin only.
// Under api/outages so one routing rule (/api/outages -> Outages) covers the whole service.
[ApiController]
[Route("api/outages")]
[Authorize(Roles = AppRoles.Admin)]
public class OutageReportsController(IOutageInsightService insight) : AppControllerBase
{
    // GET api/outages/summary
    [HttpGet("summary")]
    public async Task<ActionResult<OutageSummaryDto>> GetSummary() =>
        FromResult(await insight.GetSummaryAsync());

    // GET api/outages/activity?take=50 (newest outage status changes first)
    [HttpGet("activity")]
    public async Task<ActionResult<List<AuditLogDto>>> GetActivity([FromQuery] int take = 50) =>
        FromResult(await insight.GetActivityAsync(take));
}
