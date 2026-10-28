using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using ThorntonEnergy.Customers.Api.Models;

namespace ThorntonEnergy.Customers.Api.Data;

public static class CustomersSeeder
{
    // Same shape as the "ratePlans" and "customers" parts of the monolith's seed.json
    private class SeedFile
    {
        public List<RatePlan> RatePlans { get; set; } = [];
        public List<Customer> Customers { get; set; } = [];
    }

    public static async Task SeedAsync(CustomersDbContext db, string contentRootPath)
    {
        // Only seed an empty database
        if (await db.Customers.AnyAsync()) return;

        var path = Path.Combine(contentRootPath, "Data", "seed.json");
        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,                                            // "firstName" -> FirstName
            Converters = { new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower) } // "active" -> Active
        };

        var seed = JsonSerializer.Deserialize<SeedFile>(await File.ReadAllTextAsync(path), options)
            ?? throw new InvalidOperationException("seed.json is empty.");

        // Keep the original ids: users, bills, outages and meters in the other services all point at these customer ids.
        // SQL Server normally picks ids itself; IDENTITY_INSERT lets us choose them. It only lasts for one connection,
        // so everything runs in one transaction (which also means all-or-nothing).
        var strategy = db.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            db.ChangeTracker.Clear();
            await using var transaction = await db.Database.BeginTransactionAsync();

            // Plans before customers, so every customer's plan already exists
            db.RatePlans.AddRange(seed.RatePlans);
            await db.Database.ExecuteSqlRawAsync("SET IDENTITY_INSERT [RatePlans] ON");
            await db.SaveChangesAsync();
            await db.Database.ExecuteSqlRawAsync("SET IDENTITY_INSERT [RatePlans] OFF");

            db.Customers.AddRange(seed.Customers);
            await db.Database.ExecuteSqlRawAsync("SET IDENTITY_INSERT [Customers] ON");
            await db.SaveChangesAsync();
            await db.Database.ExecuteSqlRawAsync("SET IDENTITY_INSERT [Customers] OFF");

            await transaction.CommitAsync();
        });
    }
}
