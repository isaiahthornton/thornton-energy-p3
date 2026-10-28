using Microsoft.EntityFrameworkCore;
using ThorntonEnergy.Messages.Api.Models;

namespace ThorntonEnergy.Messages.Api.Data;

public class MessagesDbContext(DbContextOptions<MessagesDbContext> options) : DbContext(options)
{
    public DbSet<ContactMessage> ContactMessages => Set<ContactMessage>();

     protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Store enums as readable text ("Unpaid") instead of numbers (0)
        modelBuilder.Entity<ContactMessage>().Property(m => m.Topic).HasConversion<string>().HasMaxLength(20);
        modelBuilder.Entity<ContactMessage>().Property(m => m.Status).HasConversion<string>().HasMaxLength(20);
    }
}