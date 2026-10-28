using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using ThorntonEnergy.Billing.Api.Models;

namespace ThorntonEnergy.Billing.Api.Data;

public static class BillingSeeder
{
    // Same shape as the "bills" part of the monolith's seed.json
    private class SeedFile
    {
        public List<Bill> Bills { get; set; } = [];
    }

    public static async Task SeedAsync(BillingDbContext db, string contentRootPath)
    {
        // Only seed an empty database
        if (await db.Bills.AnyAsync()) return;

        var path = Path.Combine(contentRootPath, "Data", "seed.json");
        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,                                            // "amountDue" -> AmountDue
            Converters = { new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower) } // "unpaid" -> Unpaid
        };

        var seed = JsonSerializer.Deserialize<SeedFile>(await File.ReadAllTextAsync(path), options)
            ?? throw new InvalidOperationException("seed.json is empty.");

        // Keep the original ids (Angular links to bills by id). SQL Server normally picks ids itself; IDENTITY_INSERT
        // lets us choose them. It only lasts for one connection, so it runs in one transaction (also all-or-nothing).
        var strategy = db.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            db.ChangeTracker.Clear();
            await using var transaction = await db.Database.BeginTransactionAsync();

            db.Bills.AddRange(seed.Bills);
            await db.Database.ExecuteSqlRawAsync("SET IDENTITY_INSERT [Bills] ON");
            await db.SaveChangesAsync();
            await db.Database.ExecuteSqlRawAsync("SET IDENTITY_INSERT [Bills] OFF");

            await transaction.CommitAsync();
        });
    }
}
