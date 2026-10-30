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

// A tiny dataset: an admin and a login for customers 1 (Jane) and 2 (John).
// The customers themselves live in the Customers service; tests use FakeCustomerAccounts instead.
public static class TestData
{
    public const string Password = "password1";

    public static void Seed(AppDbContext db)
    {
        // Low work factor so tests stay fast
        var hash = BCrypt.Net.BCrypt.HashPassword(Password, workFactor: 4);
        db.Users.AddRange(
            new User { Id = 1, Email = "admin@test.com", PasswordHash = hash, Role = UserRole.Admin },
            new User { Id = 2, Email = "jane@test.com", PasswordHash = hash, Role = UserRole.Customer, CustomerId = 1 },
            new User { Id = 3, Email = "john@test.com", PasswordHash = hash, Role = UserRole.Customer, CustomerId = 2 });

        db.SaveChanges();
    }
}
