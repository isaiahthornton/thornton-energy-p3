using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using ThorntonEnergy.Metering.Api.Models;

namespace ThorntonEnergy.Metering.Api.Data;

public static class MeteringSeeder
{
    // Same shape as the "meters" and "meterReadings" parts of the monolith's seed.json
    private class SeedFile
    {
        public List<Meter> Meters { get; set; } = [];
        public List<MeterReading> MeterReadings { get; set; } = [];
    }

    public static async Task SeedAsync(MeteringDbContext db, string contentRootPath)
    {
        // Only seed an empty database
        if (await db.Meters.AnyAsync()) return;

        var path = Path.Combine(contentRootPath, "Data", "seed.json");
        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,                                            // "meterNumber" -> MeterNumber
            Converters = { new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower) } // "active" -> Active
        };

        var seed = JsonSerializer.Deserialize<SeedFile>(await File.ReadAllTextAsync(path), options)
            ?? throw new InvalidOperationException("seed.json is empty.");

        // Keep the original ids: readings point at meters by id, and Angular already uses these meter ids.
        // SQL Server normally picks ids itself; IDENTITY_INSERT lets us choose them. It only lasts for one
        // connection, so everything runs in one transaction (which also means all-or-nothing).
        var strategy = db.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            db.ChangeTracker.Clear();
            await using var transaction = await db.Database.BeginTransactionAsync();

            // Meters before readings, so every reading's meter already exists
            db.Meters.AddRange(seed.Meters);
            await db.Database.ExecuteSqlRawAsync("SET IDENTITY_INSERT [Meters] ON");
            await db.SaveChangesAsync();
            await db.Database.ExecuteSqlRawAsync("SET IDENTITY_INSERT [Meters] OFF");

            db.MeterReadings.AddRange(seed.MeterReadings);
            await db.Database.ExecuteSqlRawAsync("SET IDENTITY_INSERT [MeterReadings] ON");
            await db.SaveChangesAsync();
            await db.Database.ExecuteSqlRawAsync("SET IDENTITY_INSERT [MeterReadings] OFF");

            await transaction.CommitAsync();
        });
    }
}
