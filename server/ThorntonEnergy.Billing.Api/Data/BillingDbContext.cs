using Microsoft.EntityFrameworkCore;
using ThorntonEnergy.Billing.Api.Models;

namespace ThorntonEnergy.Billing.Api.Data;

public class BillingDbContext(DbContextOptions<BillingDbContext> options) : DbContext(options)
{
    public DbSet<Bill> Bills => Set<Bill>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<BillingSummary> BillingSummary => Set<BillingSummary>(); // read-only: backed by the vw_BillingSummary view

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Money uses 2 decimal places
        modelBuilder.Entity<Bill>().Property(b => b.AmountDue).HasPrecision(10, 2);
        modelBuilder.Entity<Bill>().Property(b => b.EnergyCharge).HasPrecision(10, 2);
        modelBuilder.Entity<Bill>().Property(b => b.ServiceCharge).HasPrecision(10, 2);

        // Store enums as readable text ("Unpaid") instead of numbers (0)
        modelBuilder.Entity<Bill>().Property(b => b.Status).HasConversion<string>().HasMaxLength(20);

        // CustomerId is no longer a foreign key (customers live in another database), so index it ourselves:
        // "this customer's bills" is the most common query
        modelBuilder.Entity<Bill>().HasIndex(b => b.CustomerId);

        // Bills has an audit trigger. EF must be told, or SaveChanges fails (EF reads results with an OUTPUT clause,
        // which SQL Server does not allow on a table that has triggers).
        modelBuilder.Entity<Bill>().ToTable(tb => tb.HasTrigger("trg_Bills_Audit"));

        // A SQL view, not a table: no key, and migrations do not create it (the BillingSqlObjects migration does, with SQL)
        modelBuilder.Entity<BillingSummary>().HasNoKey().ToView("vw_BillingSummary");
        modelBuilder.Entity<BillingSummary>().Property(k => k.UnpaidBalance).HasPrecision(18, 2);
        modelBuilder.Entity<BillingSummary>().Property(k => k.OverdueBalance).HasPrecision(18, 2);
        modelBuilder.Entity<BillingSummary>().Property(k => k.RevenueThisMonth).HasPrecision(18, 2);
        modelBuilder.Entity<BillingSummary>().Property(k => k.AvgKwhLastMonth).HasPrecision(18, 1);

        modelBuilder.Entity<AuditLog>().Property(a => a.TableName).HasMaxLength(50);
        modelBuilder.Entity<AuditLog>().Property(a => a.ColumnName).HasMaxLength(50);
        modelBuilder.Entity<AuditLog>().Property(a => a.OldValue).HasMaxLength(100);
        modelBuilder.Entity<AuditLog>().Property(a => a.NewValue).HasMaxLength(100);
        modelBuilder.Entity<AuditLog>().Property(a => a.ChangedBy).HasMaxLength(256);
    }
}
