using Microsoft.EntityFrameworkCore;
using ThorntonEnergy.Api.Models;

namespace ThorntonEnergy.Api.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<RatePlan> RatePlans => Set<RatePlan>();
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<Meter> Meters => Set<Meter>();
    public DbSet<MeterReading> MeterReadings => Set<MeterReading>();
    public DbSet<Bill> Bills => Set<Bill>();
    public DbSet<Outage> Outages => Set<Outage>();
    // Messages moved to ThorntonEnergy.Messages.Api. The table stays until its rows are copied to the
    // ThorntonMessages database; then remove this line and add a migration that drops it.
    public DbSet<ContactMessage> ContactMessages => Set<ContactMessage>();
    public DbSet<User> Users => Set<User>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<AdminKpis> AdminKpis => Set<AdminKpis>(); // read-only: backed by the vw_AdminKpis view

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Money uses 2 decimal places; the per-kWh rate needs 4 (e.g. 0.1450)
        modelBuilder.Entity<RatePlan>().Property(r => r.PricePerKwh).HasPrecision(10, 4);
        modelBuilder.Entity<RatePlan>().Property(r => r.MonthlyFee).HasPrecision(10, 2);
        modelBuilder.Entity<Bill>().Property(b => b.AmountDue).HasPrecision(10, 2);
        modelBuilder.Entity<Bill>().Property(b => b.EnergyCharge).HasPrecision(10, 2);
        modelBuilder.Entity<Bill>().Property(b => b.ServiceCharge).HasPrecision(10, 2);

        // Store enums as readable text ("Unpaid") instead of numbers (0)
        modelBuilder.Entity<Bill>().Property(b => b.Status).HasConversion<string>().HasMaxLength(20);
        modelBuilder.Entity<Customer>().Property(c => c.Status).HasConversion<string>().HasMaxLength(20);
        modelBuilder.Entity<Meter>().Property(m => m.Status).HasConversion<string>().HasMaxLength(20);
        modelBuilder.Entity<Outage>().Property(o => o.Status).HasConversion<string>().HasMaxLength(20);
        modelBuilder.Entity<ContactMessage>().Property(m => m.Topic).HasConversion<string>().HasMaxLength(20);
        modelBuilder.Entity<ContactMessage>().Property(m => m.Status).HasConversion<string>().HasMaxLength(20);
        modelBuilder.Entity<User>().Property(u => u.Role).HasConversion<string>().HasMaxLength(20);

        // Map coordinates: 6 decimal places is about 10 cm, plenty for a street address
        modelBuilder.Entity<Customer>().Property(c => c.Latitude).HasPrecision(9, 6);
        modelBuilder.Entity<Customer>().Property(c => c.Longitude).HasPrecision(9, 6);

        // No duplicates allowed
        modelBuilder.Entity<Customer>().HasIndex(c => c.AccountNumber).IsUnique();
        modelBuilder.Entity<Meter>().HasIndex(m => m.MeterNumber).IsUnique();
        modelBuilder.Entity<User>().HasIndex(u => u.Email).IsUnique();

        // These tables have audit triggers. EF must be told, or SaveChanges fails (EF reads results with an OUTPUT clause,
        // which SQL Server does not allow on a table that has triggers).
        modelBuilder.Entity<Bill>().ToTable(tb => tb.HasTrigger("trg_Bills_Audit"));
        modelBuilder.Entity<Customer>().ToTable(tb => tb.HasTrigger("trg_Customers_Audit"));
        modelBuilder.Entity<Outage>().ToTable(tb => tb.HasTrigger("trg_Outages_Audit"));

        // A SQL view, not a table: no key, and migrations do not create it (the AdminInsight migration does, with SQL)
        modelBuilder.Entity<AdminKpis>().HasNoKey().ToView("vw_AdminKpis");
        modelBuilder.Entity<AdminKpis>().Property(k => k.UnpaidBalance).HasPrecision(18, 2);
        modelBuilder.Entity<AdminKpis>().Property(k => k.OverdueBalance).HasPrecision(18, 2);
        modelBuilder.Entity<AdminKpis>().Property(k => k.RevenueThisMonth).HasPrecision(18, 2);
        modelBuilder.Entity<AdminKpis>().Property(k => k.AvgKwhLastMonth).HasPrecision(18, 1);

        modelBuilder.Entity<AuditLog>().Property(a => a.TableName).HasMaxLength(50);
        modelBuilder.Entity<AuditLog>().Property(a => a.ColumnName).HasMaxLength(50);
        modelBuilder.Entity<AuditLog>().Property(a => a.OldValue).HasMaxLength(100);
        modelBuilder.Entity<AuditLog>().Property(a => a.NewValue).HasMaxLength(100);
        modelBuilder.Entity<AuditLog>().Property(a => a.ChangedBy).HasMaxLength(256);

        // Deleting a rate plan must not delete its customers
        modelBuilder.Entity<Customer>()
            .HasOne(c => c.RatePlan)
            .WithMany(r => r.Customers)
            .HasForeignKey(c => c.RatePlanId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}