using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using ThorntonEnergy.Api.Models;
using ThorntonEnergy.Shared.Auth;

namespace ThorntonEnergy.Api.Data;

public static class DbSeeder
{
    // Users in seed.json have a plain "password"; the database stores a hash instead
    private record SeedUser(int Id, string Email, string Password, UserRole Role, int? CustomerId);

    // Same shape as seed.json (copied from client/db.seed.json)
    private class SeedFile
    {
        public List<SeedUser> Users { get; set; } = [];
        public List<RatePlan> RatePlans { get; set; } = [];
        public List<Customer> Customers { get; set; } = [];
        public List<Meter> Meters { get; set; } = [];
        public List<MeterReading> MeterReadings { get; set; } = [];
        public List<Bill> Bills { get; set; } = [];
        public List<Outage> Outages { get; set; } = [];
    }

    public static async Task SeedAsync(AppDbContext db, string contentRootPath)
    {
        // Only seed an empty database
        if (await db.Users.AnyAsync()) return;

        var path = Path.Combine(contentRootPath, "Data", "seed.json");
        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,                                     // "firstName" -> FirstName
            Converters = { new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower) } // "in_progress" -> InProgress
        };

        var seed = JsonSerializer.Deserialize<SeedFile>(await File.ReadAllTextAsync(path), options)
            ?? throw new InvalidOperationException("seed.json is empty.");

        var users = seed.Users.Select(u => new User
        {
            Id = u.Id,
            Email = u.Email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(u.Password),
            Role = u.Role,
            CustomerId = u.CustomerId
        }).ToList();

        // One transaction: either every table is filled or none are
        var strategy = db.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            db.ChangeTracker.Clear();
            await using var transaction = await db.Database.BeginTransactionAsync();

            // Parents before children, so every foreign key already exists
            await InsertWithIdsAsync(db, seed.RatePlans);
            await InsertWithIdsAsync(db, seed.Customers);
            await InsertWithIdsAsync(db, seed.Meters);
            await InsertWithIdsAsync(db, seed.MeterReadings);
            await InsertWithIdsAsync(db, seed.Bills);
            await InsertWithIdsAsync(db, seed.Outages);
            await InsertWithIdsAsync(db, users);

            await transaction.CommitAsync();
        });
    }

    // SQL Server normally picks Id values itself; IDENTITY_INSERT lets us keep the ids from v1
    private static async Task InsertWithIdsAsync<T>(AppDbContext db, List<T> rows) where T : class
    {
        var table = db.Model.FindEntityType(typeof(T))!.GetTableName();

        db.Set<T>().AddRange(rows);
        await db.Database.ExecuteSqlRawAsync("SET IDENTITY_INSERT [" + table + "] ON");
        await db.SaveChangesAsync();
        await db.Database.ExecuteSqlRawAsync("SET IDENTITY_INSERT [" + table + "] OFF");
    }
}