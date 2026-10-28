using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ThorntonEnergy.Billing.Api.Dtos;
using ThorntonEnergy.Billing.Api.Services;
using ThorntonEnergy.Shared.Auth;
using ThorntonEnergy.Shared.Controllers;

namespace ThorntonEnergy.Billing.Api.Controllers;

// The billing numbers for the admin dashboard. The monolith's /api/admin endpoints call these and combine them
// with its own customer and outage numbers, so Angular keeps calling /api/admin as before. Admin only.
// Under api/bills so one routing rule (/api/bills -> Billing) covers the whole service.
[ApiController]
[Route("api/bills")]
[Authorize(Roles = AppRoles.Admin)]
public class BillingReportsController(IBillingInsightService insight) : AppControllerBase
{
    // GET api/bills/summary
    [HttpGet("summary")]
    public async Task<ActionResult<BillingSummaryDto>> GetSummary() =>
        FromResult(await insight.GetSummaryAsync());

    // GET api/bills/usage-trend?months=12
    [HttpGet("usage-trend")]
    public async Task<ActionResult<List<MonthlyUsageDto>>> GetUsageTrend([FromQuery] int months = 12) =>
        FromResult(await insight.GetUsageTrendAsync(months));

    // GET api/bills/activity?take=50 (newest bill status changes first)
    [HttpGet("activity")]
    public async Task<ActionResult<List<AuditLogDto>>> GetActivity([FromQuery] int take = 50) =>
        FromResult(await insight.GetActivityAsync(take));
}
