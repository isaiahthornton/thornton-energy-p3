using ThorntonEnergy.Api.Dtos;
using ThorntonEnergy.Api.Services;

namespace ThorntonEnergy.Api.Tests.Helpers;

// Stands in for the Outages service, so tests never make HTTP calls. Set Activity to control the outage changes it returns.
public class FakeOutagesClient : IOutagesClient
{
    public List<AuditLogDto> Activity { get; set; } = [];

    public Task<OutageSummaryDto> GetSummaryAsync() => Task.FromResult(new OutageSummaryDto(OpenOutages: 1));

    // Like the real service: newest first, at most "take" rows
    public Task<List<AuditLogDto>> GetActivityAsync(int take) =>
        Task.FromResult(Activity.OrderByDescending(a => a.ChangedAt).Take(take).ToList());
}
