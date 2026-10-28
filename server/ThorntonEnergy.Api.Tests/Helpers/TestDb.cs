using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using ThorntonEnergy.Api.Data;
using ThorntonEnergy.Api.Models;
using ThorntonEnergy.Shared.Auth;

namespace ThorntonEnergy.Api.Tests.Helpers;

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
        new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options);

    public void Dispose()
    {
        Context.Dispose();
        _connection.Dispose();
    }
}

// A tiny dataset: 1 plan, 2 customers and a login for each (+ an admin)
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

        db.SaveChanges();
    }
}
