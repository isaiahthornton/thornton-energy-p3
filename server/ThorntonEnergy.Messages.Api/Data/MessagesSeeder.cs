using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using ThorntonEnergy.Messages.Api.Models;

namespace ThorntonEnergy.Messages.Api.Data;

public static class MessagesSeeder
{
    // Same shape as the "messages" part of the monolith's seed.json
    private class SeedFile
    {
        public List<ContactMessage> Messages { get; set; } = [];
    }

    public static async Task SeedAsync(MessagesDbContext db, string contentRootPath)
    {
        // Only seed an empty inbox
        if (await db.ContactMessages.AnyAsync()) return;

        var path = Path.Combine(contentRootPath, "Data", "seed.json");
        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,                                            // "sentAt" -> SentAt
            Converters = { new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower) } // "billing" -> Billing
        };

        var seed = JsonSerializer.Deserialize<SeedFile>(await File.ReadAllTextAsync(path), options)
            ?? throw new InvalidOperationException("seed.json is empty.");

        // Nothing else refers to message ids, so let SQL Server number them (no IDENTITY_INSERT needed)
        foreach (var message in seed.Messages) message.Id = 0;

        db.ContactMessages.AddRange(seed.Messages);
        await db.SaveChangesAsync();
    }
}
