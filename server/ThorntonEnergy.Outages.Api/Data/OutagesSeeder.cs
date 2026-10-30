using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using ThorntonEnergy.Outages.Api.Models;

namespace ThorntonEnergy.Outages.Api.Data;

public static class OutagesSeeder
{
    // Same shape as the "outages" part of the monolith's seed.json
    private class SeedFile
    {
        public List<Outage> Outages { get; set; } = [];
    }

    public static async Task SeedAsync(OutagesDbContext db, string contentRootPath)
    {
        // Only seed an empty database
        if (await db.Outages.AnyAsync()) return;

        var path = Path.Combine(contentRootPath, "Data", "seed.json");
        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,                                            // "timeReported" -> TimeReported
            Converters = { new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower) } // "in_progress" -> InProgress
        };

        var seed = JsonSerializer.Deserialize<SeedFile>(await File.ReadAllTextAsync(path), options)
            ?? throw new InvalidOperationException("seed.json is empty.");

        // Keep the original ids (Angular links to outages by id). SQL Server normally picks ids itself; IDENTITY_INSERT
        // lets us choose them. It only lasts for one connection, so it runs in one transaction (also all-or-nothing).
        var strategy = db.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            db.ChangeTracker.Clear();
            await using var transaction = await db.Database.BeginTransactionAsync();

            db.Outages.AddRange(seed.Outages);
            await db.Database.ExecuteSqlRawAsync("SET IDENTITY_INSERT [Outages] ON");
            await db.SaveChangesAsync();
            await db.Database.ExecuteSqlRawAsync("SET IDENTITY_INSERT [Outages] OFF");

            await transaction.CommitAsync();
        });
    }
}
