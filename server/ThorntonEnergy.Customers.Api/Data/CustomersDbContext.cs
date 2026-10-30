using Microsoft.EntityFrameworkCore;
using ThorntonEnergy.Customers.Api.Models;

namespace ThorntonEnergy.Customers.Api.Data;

public class CustomersDbContext(DbContextOptions<CustomersDbContext> options) : DbContext(options)
{
    public DbSet<RatePlan> RatePlans => Set<RatePlan>();
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<CustomerSummary> CustomerSummary => Set<CustomerSummary>(); // read-only: backed by the vw_CustomerSummary view

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Money uses 2 decimal places; the per-kWh rate needs 4 (e.g. 0.1450)
        modelBuilder.Entity<RatePlan>().Property(r => r.PricePerKwh).HasPrecision(10, 4);
        modelBuilder.Entity<RatePlan>().Property(r => r.MonthlyFee).HasPrecision(10, 2);

        // Store enums as readable text ("Active") instead of numbers (0)
        modelBuilder.Entity<Customer>().Property(c => c.Status).HasConversion<string>().HasMaxLength(20);

        // Map coordinates: 6 decimal places is about 10 cm, plenty for a street address
        modelBuilder.Entity<Customer>().Property(c => c.Latitude).HasPrecision(9, 6);
        modelBuilder.Entity<Customer>().Property(c => c.Longitude).HasPrecision(9, 6);

        // No duplicate account numbers
        modelBuilder.Entity<Customer>().HasIndex(c => c.AccountNumber).IsUnique();

        // Customers has an audit trigger. EF must be told, or SaveChanges fails (EF reads results with an OUTPUT clause,
        // which SQL Server does not allow on a table that has triggers).
        modelBuilder.Entity<Customer>().ToTable(tb => tb.HasTrigger("trg_Customers_Audit"));

        // A SQL view, not a table: no key, and migrations do not create it (the CustomersSqlObjects migration does, with SQL)
        modelBuilder.Entity<CustomerSummary>().HasNoKey().ToView("vw_CustomerSummary");

        modelBuilder.Entity<AuditLog>().Property(a => a.TableName).HasMaxLength(50);
        modelBuilder.Entity<AuditLog>().Property(a => a.ColumnName).HasMaxLength(50);
        modelBuilder.Entity<AuditLog>().Property(a => a.OldValue).HasMaxLength(100);
        modelBuilder.Entity<AuditLog>().Property(a => a.NewValue).HasMaxLength(100);
        modelBuilder.Entity<AuditLog>().Property(a => a.ChangedBy).HasMaxLength(256);

        // Deleting a rate plan must not delete its customers (and is refused while any customer is on it)
        modelBuilder.Entity<Customer>()
            .HasOne(c => c.RatePlan)
            .WithMany(r => r.Customers)
            .HasForeignKey(c => c.RatePlanId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
