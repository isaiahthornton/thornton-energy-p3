using ThorntonEnergy.Api.Dtos;
using ThorntonEnergy.Api.Models;
using ThorntonEnergy.Api.Services;
using ThorntonEnergy.Api.Tests.Helpers;
using ThorntonEnergy.Shared.Common;

namespace ThorntonEnergy.Api.Tests.Unit;

// The activity feed is put together from four places: the Customers, Billing and Outages services, plus this
// API's own history table (changes recorded before the split).
// (The KPIs read a SQL Server view, which SQLite does not have, so they are checked against the running app instead.)
public class AdminInsightServiceTests : IDisposable
{
    private readonly TestDb _db = new();
    private readonly FakeCustomersClient _customers = new();
    private readonly FakeBillingClient _billing = new();
    private readonly FakeOutagesClient _outages = new();

    public void Dispose() => _db.Dispose();

    private AdminInsightService CreateService() => new(_db.Context, _customers, _billing, _outages);

    // A row in this API's history table (from before the split)
    private void AddHistory(int id, string table, DateTime at)
    {
        _db.Context.AuditLogs.Add(new AuditLog
        {
            Id = id, TableName = table, RecordId = 1, ColumnName = "Status", OldValue = "A", NewValue = "B", ChangedAt = at,
        });
        _db.Context.SaveChanges();
    }

    private static AuditLogDto BillChange(int id, DateTime at) => new(id, "Bills", 1, "Status", "Unpaid", "Paid", at, "admin@test.com");
    private static AuditLogDto CustomerChange(int id, DateTime at) => new(id, "Customers", 1, "Status", "Active", "Suspended", at, "admin@test.com");
    private static AuditLogDto OutageChange(int id, DateTime at) => new(id, "Outages", 1, "Status", "Reported", "Resolved", at, "admin@test.com");

    [Fact]
    public async Task Activity_MergesAllFourSourcesNewestFirst()
    {
        // Arrange: old history here, then changes alternating between the services (ids repeat across them)
        AddHistory(1, "Bills", new DateTime(2026, 4, 1));
        _customers.Activity = [CustomerChange(1, new DateTime(2026, 5, 1)), CustomerChange(2, new DateTime(2026, 5, 4))];
        _billing.Activity = [BillChange(1, new DateTime(2026, 5, 2)), BillChange(2, new DateTime(2026, 5, 5))];
        _outages.Activity = [OutageChange(1, new DateTime(2026, 5, 3))];

        // Act
        var result = await CreateService().GetActivityAsync(50);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(["Bills", "Customers", "Outages", "Bills", "Customers", "Bills"], result.Value!.Select(a => a.TableName));
        Assert.Equal(new DateTime(2026, 4, 1), result.Value!.Last().ChangedAt); // the old history comes last
    }

    [Fact]
    public async Task Activity_KeepsOnlyTheNewestTakeRowsOverall()
    {
        // Arrange: the two newest changes are both bills
        _customers.Activity = [CustomerChange(1, new DateTime(2026, 5, 1))];
        _outages.Activity = [OutageChange(1, new DateTime(2026, 5, 1, 12, 0, 0))];
        _billing.Activity = [BillChange(1, new DateTime(2026, 5, 2)), BillChange(2, new DateTime(2026, 5, 3))];

        // Act
        var result = await CreateService().GetActivityAsync(2);

        // Assert
        Assert.Equal(2, result.Value!.Count);
        Assert.All(result.Value, a => Assert.Equal("Bills", a.TableName));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(201)]
    public async Task Activity_OutOfRangeTake_IsInvalid(int take)
    {
        // Act
        var result = await CreateService().GetActivityAsync(take);

        // Assert
        Assert.Equal(ResultStatus.Invalid, result.Status);
    }
}
