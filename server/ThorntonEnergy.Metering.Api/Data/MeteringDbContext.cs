using Microsoft.EntityFrameworkCore;
using ThorntonEnergy.Metering.Api.Models;

namespace ThorntonEnergy.Metering.Api.Data;

public class MeteringDbContext(DbContextOptions<MeteringDbContext> options) : DbContext(options)
{
    public DbSet<Meter> Meters => Set<Meter>();
    public DbSet<MeterReading> MeterReadings => Set<MeterReading>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Store enums as readable text ("Active") instead of numbers (0)
        modelBuilder.Entity<Meter>().Property(m => m.Status).HasConversion<string>().HasMaxLength(20);

        // No duplicate meter numbers
        modelBuilder.Entity<Meter>().HasIndex(m => m.MeterNumber).IsUnique();

        // CustomerId is no longer a foreign key (customers live in another database), so index it ourselves:
        // "this customer's meters" is the most common query
        modelBuilder.Entity<Meter>().HasIndex(m => m.CustomerId);
    }
}
