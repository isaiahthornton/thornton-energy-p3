using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using ThorntonEnergy.Metering.Api.Data;
using ThorntonEnergy.Metering.Api.Models;

namespace ThorntonEnergy.Metering.Api.Tests;

// A tiny dataset: customer 1 (Jane) has meter 1, which already reads 1000 on 2026-01-01. Customer 2 has no meters.
// Customers themselves live in the monolith; here they are only ids (the same ones the test tokens carry).
public static class TestData
{
    public static void Seed(MeteringDbContext db)
    {
        db.Meters.Add(new Meter { Id = 1, CustomerId = 1, MeterNumber = "M-1", InstallationDate = new DateOnly(2025, 1, 1), Status = MeterStatus.Active });
        db.MeterReadings.Add(new MeterReading { Id = 1, MeterId = 1, ReadingDate = new DateOnly(2026, 1, 1), ReadingValue = 1000 });
        db.SaveChanges();
    }
}

// A throw-away in-memory database for one unit test (the connection must stay open or SQLite forgets everything)
public sealed class TestDb : IDisposable
{
    private readonly SqliteConnection _connection = new("DataSource=:memory:");

    public MeteringDbContext Context { get; }

    public TestDb()
    {
        _connection.Open();
        Context = new MeteringDbContext(new DbContextOptionsBuilder<MeteringDbContext>().UseSqlite(_connection).Options);
        Context.Database.EnsureCreated(); // builds the tables from the EF model
        TestData.Seed(Context);
    }

    public void Dispose()
    {
        Context.Dispose();
        _connection.Dispose();
    }
}
