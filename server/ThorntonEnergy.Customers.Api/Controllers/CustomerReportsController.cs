using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ThorntonEnergy.Customers.Api.Dtos;
using ThorntonEnergy.Customers.Api.Services;
using ThorntonEnergy.Shared.Auth;
using ThorntonEnergy.Shared.Controllers;

namespace ThorntonEnergy.Customers.Api.Controllers;

// The customer numbers for the admin dashboard. The monolith's /api/admin endpoints call these and combine them
// with the bill and outage numbers, so Angular keeps calling /api/admin as before. Admin only.
// Under api/customers so one routing rule (/api/customers -> Customers) covers the whole service.
[ApiController]
[Route("api/customers")]
[Authorize(Roles = AppRoles.Admin)]
public class CustomerReportsController(ICustomerInsightService insight) : AppControllerBase
{
    // GET api/customers/summary
    [HttpGet("summary")]
    public async Task<ActionResult<CustomerSummaryDto>> GetSummary() =>
        FromResult(await insight.GetSummaryAsync());

    // GET api/customers/activity?take=50 (newest customer status / rate plan changes first)
    [HttpGet("activity")]
    public async Task<ActionResult<List<AuditLogDto>>> GetActivity([FromQuery] int take = 50) =>
        FromResult(await insight.GetActivityAsync(take));
}
