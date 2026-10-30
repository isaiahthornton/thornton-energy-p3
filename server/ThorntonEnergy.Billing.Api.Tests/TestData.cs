using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using ThorntonEnergy.Billing.Api.Data;
using ThorntonEnergy.Billing.Api.Models;
using ThorntonEnergy.Billing.Api.Services;

namespace ThorntonEnergy.Billing.Api.Tests;

// The real BillingDbContext, but SQLite has no SQL Server "rowversion" column, so we tell EF the
// application sets it (it is just an empty value in tests; the concurrency check is not under test here).
public sealed class SqliteBillingDbContext(DbContextOptions<BillingDbContext> options) : BillingDbContext(options)
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.Entity<Bill>().Property(b => b.RowVersion).ValueGeneratedNever();
    }
}

// Stands in for the customers service: customers 1 (Jane) and 2 (John) are both on a 0.10/kWh + 10.00 plan
public class FakeCustomerDirectory : ICustomerDirectory
{
    public Task<RatePlanPrices?> GetRatePlanPricesAsync(int customerId) =>
        Task.FromResult(customerId is 1 or 2 ? new RatePlanPrices(0.10m, 10m) : null);
}

// A tiny dataset: one unpaid bill each for Jane (bill 1) and John (bill 2), both long past due
public static class TestData
{
    public static void Seed(BillingDbContext db)
    {
        db.Bills.AddRange(
            NewBill(1, 1, new DateOnly(2026, 1, 1), BillStatus.Unpaid),
            NewBill(2, 2, new DateOnly(2026, 1, 1), BillStatus.Unpaid));
        db.SaveChanges();
    }

    private static Bill NewBill(int id, int customerId, DateOnly periodStart, BillStatus status) => new()
    {
        Id = id, CustomerId = customerId, PeriodStart = periodStart, PeriodEnd = periodStart.AddDays(30),
        DueDate = periodStart.AddDays(30 + 21), KwhUsed = 100, EnergyCharge = 10m, ServiceCharge = 10m, AmountDue = 20m, Status = status,
    };
}

// A throw-away in-memory database for one unit test (the connection must stay open or SQLite forgets everything)
public sealed class TestDb : IDisposable
{
    private readonly SqliteConnection _connection = new("DataSource=:memory:");

    public BillingDbContext Context { get; }

    public TestDb()
    {
        _connection.Open();
        Context = Create(_connection);
        Context.Database.EnsureCreated(); // builds the tables from the EF model
        TestData.Seed(Context);
    }

    public static BillingDbContext Create(SqliteConnection connection) =>
        new SqliteBillingDbContext(new DbContextOptionsBuilder<BillingDbContext>().UseSqlite(connection).Options);

    public void Dispose()
    {
        Context.Dispose();
        _connection.Dispose();
    }
}
