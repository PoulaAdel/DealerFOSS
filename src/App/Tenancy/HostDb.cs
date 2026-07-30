// HostDb — the control-plane database. Exactly one exists per
// deployment and it knows only which tenants exist and how to reach them.
//
// Use:  injected by TenantResolver. Ordinary feature code never touches it.
// Edit: adding a business table here is a design error — see ADR-003. Schema
//       changes need a migration in this project (Migrations/).

using Microsoft.EntityFrameworkCore;

namespace OpenDealer360.Tenancy;

/// <summary>
/// The control-plane catalog context. Holds tenant routing only (doc 04 §2).
/// There is exactly one host catalog per deployment.
/// </summary>
public sealed class HostDb(DbContextOptions<HostDb> options)
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
