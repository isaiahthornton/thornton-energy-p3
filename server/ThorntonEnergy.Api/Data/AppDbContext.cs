using Microsoft.EntityFrameworkCore;
using ThorntonEnergy.Api.Models;

namespace ThorntonEnergy.Api.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<RatePlan> RatePlans => Set<RatePlan>();
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<User> Users => Set<User>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<AdminKpis> AdminKpis => Set<AdminKpis>(); // read-only: backed by the vw_AdminKpis view

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Money uses 2 decimal places; the per-kWh rate needs 4 (e.g. 0.1450)
        modelBuilder.Entity<RatePlan>().Property(r => r.PricePerKwh).HasPrecision(10, 4);
        modelBuilder.Entity<RatePlan>().Property(r => r.MonthlyFee).HasPrecision(10, 2);

        // Store enums as readable text ("Active") instead of numbers (0)
        modelBuilder.Entity<Customer>().Property(c => c.Status).HasConversion<string>().HasMaxLength(20);
        modelBuilder.Entity<User>().Property(u => u.Role).HasConversion<string>().HasMaxLength(20);

        // Map coordinates: 6 decimal places is about 10 cm, plenty for a street address
        modelBuilder.Entity<Customer>().Property(c => c.Latitude).HasPrecision(9, 6);
        modelBuilder.Entity<Customer>().Property(c => c.Longitude).HasPrecision(9, 6);

        // No duplicates allowed
        modelBuilder.Entity<Customer>().HasIndex(c => c.AccountNumber).IsUnique();
        modelBuilder.Entity<User>().HasIndex(u => u.Email).IsUnique();

        // Customers has an audit trigger. EF must be told, or SaveChanges fails (EF reads results with an OUTPUT clause,
        // which SQL Server does not allow on a table that has triggers).
        modelBuilder.Entity<Customer>().ToTable(tb => tb.HasTrigger("trg_Customers_Audit"));

        // A SQL view, not a table: no key, and migrations do not create it (the AdminInsight migration does, with SQL)
        modelBuilder.Entity<AdminKpis>().HasNoKey().ToView("vw_AdminKpis");

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