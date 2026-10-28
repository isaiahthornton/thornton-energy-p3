using Microsoft.EntityFrameworkCore;
using ThorntonEnergy.Outages.Api.Models;

namespace ThorntonEnergy.Outages.Api.Data;

public class OutagesDbContext(DbContextOptions<OutagesDbContext> options) : DbContext(options)
{
    public DbSet<Outage> Outages => Set<Outage>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<OutageSummary> OutageSummary => Set<OutageSummary>(); // read-only: backed by the vw_OutageSummary view

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Store enums as readable text ("InProgress") instead of numbers (1)
        modelBuilder.Entity<Outage>().Property(o => o.Status).HasConversion<string>().HasMaxLength(20);

        // CustomerId is no longer a foreign key (customers live in another database), so index it ourselves:
        // "this customer's outages" is the most common query
        modelBuilder.Entity<Outage>().HasIndex(o => o.CustomerId);

        // Outages has an audit trigger. EF must be told, or SaveChanges fails (EF reads results with an OUTPUT clause,
        // which SQL Server does not allow on a table that has triggers).
        modelBuilder.Entity<Outage>().ToTable(tb => tb.HasTrigger("trg_Outages_Audit"));

        // A SQL view, not a table: no key, and migrations do not create it (the OutagesSqlObjects migration does, with SQL)
        modelBuilder.Entity<OutageSummary>().HasNoKey().ToView("vw_OutageSummary");

        modelBuilder.Entity<AuditLog>().Property(a => a.TableName).HasMaxLength(50);
        modelBuilder.Entity<AuditLog>().Property(a => a.ColumnName).HasMaxLength(50);
        modelBuilder.Entity<AuditLog>().Property(a => a.OldValue).HasMaxLength(100);
        modelBuilder.Entity<AuditLog>().Property(a => a.NewValue).HasMaxLength(100);
        modelBuilder.Entity<AuditLog>().Property(a => a.ChangedBy).HasMaxLength(256);
    }
}
