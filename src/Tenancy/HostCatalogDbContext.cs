using Microsoft.EntityFrameworkCore;

namespace OpenDealer360.Tenancy;

/// <summary>
/// The control-plane catalog context. Holds tenant routing only (doc 04 §2).
/// There is exactly one host catalog per deployment.
/// </summary>
public sealed class HostCatalogDbContext(DbContextOptions<HostCatalogDbContext> options)
    : DbContext(options)
{
    public DbSet<TenantRecord> Tenants => Set<TenantRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var tenant = modelBuilder.Entity<TenantRecord>();
        tenant.ToTable("Tenants");
        tenant.HasKey(t => t.Id);
        tenant.Property(t => t.Id).ValueGeneratedNever();
        tenant.Property(t => t.Name).HasMaxLength(200);
        tenant.Property(t => t.Slug).HasMaxLength(100);
        tenant.HasIndex(t => t.Slug).IsUnique();
        tenant.Property(t => t.Slug).IsRequired();
        tenant.Property(t => t.ProtectedConnectionString).IsRequired();
        tenant.Property(t => t.Status).HasConversion<string>().HasMaxLength(20);
    }
}
