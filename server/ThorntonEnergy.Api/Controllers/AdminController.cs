using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ThorntonEnergy.Api.Dtos;
using ThorntonEnergy.Api.Services;
using ThorntonEnergy.Shared.Auth;
using ThorntonEnergy.Shared.Controllers;

namespace ThorntonEnergy.Api.Controllers;

// Numbers for the admin dashboard and the activity page. Admin only.
[ApiController]
[Route("api/admin")]
[Authorize(Roles = AppRoles.Admin)]
public class AdminController(IAdminInsightService insight) : AppControllerBase
{
    // GET api/admin/kpis
    [HttpGet("kpis")]
    public async Task<ActionResult<AdminKpisDto>> GetKpis() =>
        FromResult(await insight.GetKpisAsync());

    // GET api/admin/usage-trend?months=12
    [HttpGet("usage-trend")]
    public async Task<ActionResult<List<MonthlyUsageDto>>> GetUsageTrend([FromQuery] int months = 12) =>
        FromResult(await insight.GetUsageTrendAsync(months));

    // GET api/admin/activity?take=50 (newest changes first)
    [HttpGet("activity")]
    public async Task<ActionResult<List<AuditLogDto>>> GetActivity([FromQuery] int take = 50) =>
        FromResult(await insight.GetActivityAsync(take));
}
