using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using ThorntonEnergy.Api.Data;
using ThorntonEnergy.Api.Models;
using ThorntonEnergy.Shared.Auth;

namespace ThorntonEnergy.Api.Tests.Helpers;

// The real AppDbContext, but SQLite has no SQL Server "rowversion" column, so we tell EF the
// application sets it (it is just an empty value in tests; the concurrency check is not under test here).
public sealed class SqliteAppDbContext(DbContextOptions<AppDbContext> options) : AppDbContext(options)
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.Entity<Bill>().Property(b => b.RowVersion).ValueGeneratedNever();
    }
}

// A throw-away in-memory database for one test (the connection must stay open or SQLite forgets everything)
public sealed class TestDb : IDisposable
{
    private readonly SqliteConnection _connection = new("DataSource=:memory:");

    public AppDbContext Context { get; }

    public TestDb()
    {
        _connection.Open();
        Context = Create(_connection);
        Context.Database.EnsureCreated(); // builds the tables from the EF model
        TestData.Seed(Context);
    }

    public static AppDbContext Create(SqliteConnection connection) =>
        new SqliteAppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options);

    public void Dispose()
    {
        Context.Dispose();
        _connection.Dispose();
    }
}

// A tiny dataset: 1 plan, 2 customers, a login for each (+ an admin), and a few bills and meters
public static class TestData
{
    public const string Password = "password1";

    public static void Seed(AppDbContext db)
    {
        db.RatePlans.Add(new RatePlan { Id = 1, Name = "Standard", PricePerKwh = 0.10m, MonthlyFee = 10m, Description = "Test plan" });

        db.Customers.AddRange(
            new Customer
            {
                Id = 1, AccountNumber = "TE-100001", FirstName = "Jane", LastName = "Doe", Phone = "1", Email = "jane@test.com",
                ServiceAddress = "1 Main St", RatePlanId = 1, Status = AccountStatus.Active,
            },
            new Customer
            {
                Id = 2, AccountNumber = "TE-100002", FirstName = "John", LastName = "Smith", Phone = "2", Email = "john@test.com",
                ServiceAddress = "2 Main St", RatePlanId = 1, Status = AccountStatus.Active,
            });

        // Low work factor so tests stay fast
        var hash = BCrypt.Net.BCrypt.HashPassword(Password, workFactor: 4);
        db.Users.AddRange(
            new User { Id = 1, Email = "admin@test.com", PasswordHash = hash, Role = UserRole.Admin },
            new User { Id = 2, Email = "jane@test.com", PasswordHash = hash, Role = UserRole.Customer, CustomerId = 1 },
            new User { Id = 3, Email = "john@test.com", PasswordHash = hash, Role = UserRole.Customer, CustomerId = 2 });

        db.Bills.AddRange(
            // Jane: one unpaid bill that is long past due
            NewBill(1, 1, new DateOnly(2026, 1, 1), BillStatus.Unpaid),
            // John: one unpaid bill
            NewBill(2, 2, new DateOnly(2026, 1, 1), BillStatus.Unpaid));

        db.Meters.Add(new Meter { Id = 1, CustomerId = 1, MeterNumber = "M-1", InstallationDate = new DateOnly(2025, 1, 1), Status = MeterStatus.Active });
        db.MeterReadings.Add(new MeterReading { Id = 1, MeterId = 1, ReadingDate = new DateOnly(2026, 1, 1), ReadingValue = 1000 });

        db.SaveChanges();
    }

    private static Bill NewBill(int id, int customerId, DateOnly periodStart, BillStatus status) => new()
    {
        Id = id, CustomerId = customerId, PeriodStart = periodStart, PeriodEnd = periodStart.AddDays(30),
        DueDate = periodStart.AddDays(30 + 21), KwhUsed = 100, EnergyCharge = 10m, ServiceCharge = 10m, AmountDue = 20m, Status = status,
    };
}
