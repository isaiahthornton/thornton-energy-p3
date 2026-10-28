using Microsoft.EntityFrameworkCore;
using ThorntonEnergy.Api.Models;

namespace ThorntonEnergy.Api.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>(); // history from before the split (read only)

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Store enums as readable text ("Admin") instead of numbers (1)
        modelBuilder.Entity<User>().Property(u => u.Role).HasConversion<string>().HasMaxLength(20);

        // One login per email
        modelBuilder.Entity<User>().HasIndex(u => u.Email).IsUnique();

        // CustomerId is no longer a foreign key (customers live in the Customers service), so index it ourselves:
        // register checks "does this customer already have a login?"
        modelBuilder.Entity<User>().HasIndex(u => u.CustomerId);

        modelBuilder.Entity<AuditLog>().Property(a => a.TableName).HasMaxLength(50);
        modelBuilder.Entity<AuditLog>().Property(a => a.ColumnName).HasMaxLength(50);
        modelBuilder.Entity<AuditLog>().Property(a => a.OldValue).HasMaxLength(100);
        modelBuilder.Entity<AuditLog>().Property(a => a.NewValue).HasMaxLength(100);
        modelBuilder.Entity<AuditLog>().Property(a => a.ChangedBy).HasMaxLength(256);
    }
}