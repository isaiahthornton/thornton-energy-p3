using ThorntonEnergy.Outages.Api.Data;
using ThorntonEnergy.Outages.Api.Models;
using ThorntonEnergy.Outages.Api.Services;

namespace ThorntonEnergy.Outages.Api.Tests;

// Stands in for the customers service: customers 1 (Jane, on the map) and 2 (John, no coordinates yet) exist
public class FakeCustomerDirectory : ICustomerDirectory
{
    public Task<bool> ExistsAsync(int customerId) => Task.FromResult(customerId is 1 or 2);

    public Task<Dictionary<int, CustomerLocation>> GetLocationsAsync() =>
        Task.FromResult(new Dictionary<int, CustomerLocation> { [1] = new("Jane Doe", 40.2m, -74.5m) });
}

// A tiny dataset: one open outage each for Jane (outage 1) and John (outage 2)
public static class TestData
{
    public static void Seed(OutagesDbContext db)
    {
        db.Outages.AddRange(
            new Outage { Id = 1, CustomerId = 1, Address = "1 Main St", Description = "Dark", TimeReported = new DateTime(2026, 5, 1, 9, 0, 0), Status = OutageStatus.Reported },
            new Outage { Id = 2, CustomerId = 2, Address = "2 Main St", Description = "Flickering", TimeReported = new DateTime(2026, 5, 2, 9, 0, 0), Status = OutageStatus.InProgress });
        db.SaveChanges();
    }
}
