// ControlPlaneDb — persistence for the deployment's own identities, in the host
// catalog database. Owns the "control" schema and no other.
//
// Use:  injected into GlobalAdministrationService and nothing else.
// Edit: this context binds to the host catalog connection, not to any tenant's.
//       That is the separation: there is no code path by which an administrator
//       row and a dealership's business tables are open in the same context. A
//       business table added here would be a design error of the same kind as
//       one added to HostDb (ADR-003).
//
//       Why this lives in the Identity project rather than in a capability folder:
//       password verification, TOTP, and session issuance must exist in exactly
//       one place, behind the wall that exists to hold them. A second copy in
//       src/App would be reachable from every feature — which is precisely what
//       ADR-017 prevents.

using Microsoft.EntityFrameworkCore;

namespace OpenDealer360.Identity;

/// <summary>
/// Persistence for control-plane identity: administrators, their sessions, the
/// support grants they have been given, and the installation's own append-only
/// log. Lives in the host catalog (doc 04 §2), never in a tenant database.
/// </summary>
internal sealed class ControlPlaneDb(DbContextOptions<ControlPlaneDb> options)
    : DbContext(options)
{
    public const string Schema = "control";

    public DbSet<Administrator> Administrators => Set<Administrator>();

    public DbSet<AdminSession> AdminSessions => Set<AdminSession>();

    public DbSet<SupportGrant> SupportGrants => Set<SupportGrant>();

    public DbSet<ControlPlaneAudit> AuditEvents => Set<ControlPlaneAudit>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);

        modelBuilder.Entity<Administrator>(builder =>
        {
            builder.ToTable("Administrators");
            builder.HasKey(x => x.Id);
            builder.Property(x => x.Id).ValueGeneratedNever();
            builder.Property(x => x.Email).HasMaxLength(320).IsRequired();
            builder.Property(x => x.DisplayName).HasMaxLength(200).IsRequired();
            builder.HasIndex(x => x.Email).IsUnique();
            builder.Property(x => x.MfaSecretProtected).HasMaxLength(400);
            builder.Ignore(x => x.MfaEnabled);
            builder.Ignore(x => x.CanSignIn);
        });

        modelBuilder.Entity<AdminSession>(builder =>
        {
            builder.ToTable("AdminSessions");
            builder.HasKey(x => x.Id);
            builder.Property(x => x.Id).ValueGeneratedNever();
            builder.Property(x => x.TokenHash).HasMaxLength(64).IsRequired();
            builder.Property(x => x.AntiForgeryHash).HasMaxLength(64).IsRequired();
            builder.Property(x => x.DeviceSummary).HasMaxLength(200);
            builder.Ignore(x => x.IsRevoked);
            // Looked up by hash on every control-plane request.
            builder.HasIndex(x => x.TokenHash).IsUnique();
        });

        modelBuilder.Entity<SupportGrant>(builder =>
        {
            builder.ToTable("SupportGrants");
            builder.HasKey(x => x.Id);
            builder.Property(x => x.Id).ValueGeneratedNever();
            builder.Property(x => x.TenantSlug).HasMaxLength(100).IsRequired();
            builder.Property(x => x.Reason).HasMaxLength(500).IsRequired();
            builder.HasIndex(x => new { x.TenantSlug, x.GrantedAt });
            builder.HasIndex(x => x.AdministratorId);
        });

        modelBuilder.Entity<ControlPlaneAudit>(builder =>
        {
            builder.ToTable("AuditEvents");
            builder.HasKey(x => x.Id);
            builder.Property(x => x.Id).ValueGeneratedNever();
            builder.Property(x => x.Action).HasMaxLength(120).IsRequired();
            builder.Property(x => x.Outcome).HasMaxLength(20).IsRequired();
            builder.Property(x => x.TenantSlug).HasMaxLength(100);
            builder.Property(x => x.Reason).HasMaxLength(500);
            builder.HasIndex(x => x.OccurredAt);
        });
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        GuardAppendOnlyAudit();
        return base.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// ADR-016 in code rather than convention: the installation's log cannot be
    /// rewritten through this context either.
    /// </summary>
    private void GuardAppendOnlyAudit()
    {
        foreach (var entry in ChangeTracker.Entries<ControlPlaneAudit>())
        {
            if (entry.State is EntityState.Modified or EntityState.Deleted)
            {
                throw new InvalidOperationException(
                    "Audit events are append-only (ADR-016). Record a new event instead of "
                    + $"attempting to {entry.State.ToString().ToLowerInvariant()} an existing one.");
            }
        }
    }
}
