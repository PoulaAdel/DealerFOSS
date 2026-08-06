// IdentityDb — persistence for this module. Owns the "identity" schema
// and no other (ADR-014).
//
// Use:  injected into the module's own services; nothing outside Identity may
//       take a dependency on it.
// Edit: schema changes need a migration in this project. SaveChangesAsync stamps
//       audit columns centrally and refuses to modify audit history — do not
//       bypass it. Note "identity" is a reserved T-SQL word: hand-written SQL
//       must bracket it as [identity].

using Microsoft.EntityFrameworkCore;
using DealerFOSS.Identity;
using DealerFOSS.Core;

namespace DealerFOSS.Identity;

/// <summary>
/// Persistence for the Identity module. Owns the <c>identity</c> schema and no
/// other (ADR-014). Audit events are append-only: this context refuses to update
/// or delete them (ADR-016).
/// </summary>
internal sealed class IdentityDb(DbContextOptions<IdentityDb> options, IClock clock)
    : DbContext(options)
{
    public const string Schema = "identity";

    private readonly IClock _clock = clock;

    public DbSet<User> Users => Set<User>();

    public DbSet<Role> Roles => Set<Role>();

    public DbSet<UserAssignment> UserAssignments => Set<UserAssignment>();

    public DbSet<Session> Sessions => Set<Session>();

    public DbSet<AuditEvent> AuditEvents => Set<AuditEvent>();

    public DbSet<SignInChallenge> SignInChallenges => Set<SignInChallenge>();

    public DbSet<StaffEnrolment> StaffEnrolments => Set<StaffEnrolment>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);

        modelBuilder.Entity<User>(builder =>
        {
            builder.ToTable("Users");
            builder.HasKey(x => x.Id);
            builder.Property(x => x.Id).ValueGeneratedNever();
            builder.Property(x => x.Email).HasMaxLength(320).IsRequired();
            builder.Property(x => x.DisplayName).HasMaxLength(200).IsRequired();
            builder.HasIndex(x => x.Email).IsUnique();
            // Long enough for the protected form: the base32 secret wrapped in
            // AES-GCM and base64, plus the version and key id.
            builder.Property(x => x.MfaSecretProtected).HasMaxLength(400);
            builder.Ignore(x => x.MfaEnabled);
            builder.HasMany(x => x.Assignments)
                .WithOne()
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);
            builder.HasMany(x => x.RecoveryCodes)
                .WithOne()
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);
            ConfigureAudit(builder);
        });

        modelBuilder.Entity<RecoveryCode>(builder =>
        {
            builder.ToTable("RecoveryCodes");
            builder.HasKey(x => x.Id);
            builder.Property(x => x.Id).ValueGeneratedNever();
            builder.Property(x => x.CodeHash).HasMaxLength(64).IsRequired();
            builder.Ignore(x => x.IsAvailable);
            builder.HasIndex(x => new { x.UserId, x.CodeHash });
        });

        modelBuilder.Entity<StaffEnrolment>(builder =>
        {
            builder.ToTable("StaffEnrolments");
            builder.HasKey(x => x.Id);
            builder.Property(x => x.Id).ValueGeneratedNever();
            builder.Property(x => x.CodeHash).HasMaxLength(64).IsRequired();

            // The redemption path finds the live code for one account, and the
            // cleanup path finds expired ones.
            builder.HasIndex(x => new { x.UserId, x.ConsumedAt });
            builder.HasIndex(x => x.ExpiresAt);
        });

        modelBuilder.Entity<SignInChallenge>(builder =>
        {
            builder.ToTable("SignInChallenges");
            builder.HasKey(x => x.Id);
            builder.Property(x => x.Id).ValueGeneratedNever();
            builder.Property(x => x.TokenHash).HasMaxLength(64).IsRequired();
            builder.Property(x => x.DeviceSummary).HasMaxLength(200);
            // Looked up by hash on every second-factor attempt.
            builder.HasIndex(x => x.TokenHash).IsUnique();
            builder.HasIndex(x => x.ExpiresAt);
        });

        modelBuilder.Entity<Role>(builder =>
        {
            builder.ToTable("Roles");
            builder.HasKey(x => x.Id);
            builder.Property(x => x.Id).ValueGeneratedNever();
            builder.Property(x => x.Name).HasMaxLength(100).IsRequired();
            builder.HasIndex(x => x.Name).IsUnique();
            // Off for every role that already exists: an upgrade must not
            // suddenly demand a second factor nobody was told about.
            builder.Property(x => x.RequiresSecondFactor).HasDefaultValue(false);
            builder.HasMany(x => x.Permissions)
                .WithOne()
                .HasForeignKey(x => x.RoleId)
                .OnDelete(DeleteBehavior.Cascade);
            builder.Navigation(x => x.Permissions).AutoInclude();
            ConfigureAudit(builder);
        });

        modelBuilder.Entity<RolePermission>(builder =>
        {
            builder.ToTable("RolePermissions");
            builder.HasKey(x => new { x.RoleId, x.Permission });
            builder.Property(x => x.Permission).HasMaxLength(100);
        });

        modelBuilder.Entity<UserAssignment>(builder =>
        {
            builder.ToTable("UserAssignments");
            builder.HasKey(x => x.Id);
            builder.Property(x => x.Id).ValueGeneratedNever();
            builder.Property(x => x.Scope).HasConversion<string>().HasMaxLength(20);
            builder.Property(x => x.RooftopId)
                .HasConversion(
                    id => id!.Value.Value,
                    value => new RooftopId(value));
            builder.HasIndex(x => new { x.UserId, x.Scope });
            ConfigureAudit(builder);
        });

        modelBuilder.Entity<Session>(builder =>
        {
            builder.ToTable("Sessions");
            builder.HasKey(x => x.Id);
            builder.Property(x => x.Id).ValueGeneratedNever();
            builder.Property(x => x.TokenHash).HasMaxLength(64).IsRequired();
            // Sessions created before anti-forgery existed carry an empty string,
            // which no SHA-256 hex value can equal — so their writes are refused
            // and the user signs in again. That is the safe direction to fail.
            builder.Property(x => x.AntiForgeryHash).HasMaxLength(64).IsRequired();
            builder.Property(x => x.DeviceSummary).HasMaxLength(200);
            // Every request looks a session up by token hash, so this index is
            // on the hot path, and it is unique because a token identifies one
            // session or none.
            builder.HasIndex(x => x.TokenHash).IsUnique();
            builder.HasIndex(x => new { x.UserId, x.AbsoluteExpiresAt });
            ConfigureAudit(builder);
        });

        modelBuilder.Entity<AuditEvent>(builder =>
        {
            builder.ToTable("AuditEvents");
            builder.HasKey(x => x.Id);
            builder.Property(x => x.Id).ValueGeneratedNever();
            builder.Property(x => x.Action).HasMaxLength(120).IsRequired();
            builder.Property(x => x.Outcome).HasMaxLength(20).IsRequired();
            builder.Property(x => x.ResourceType).HasMaxLength(120);
            builder.Property(x => x.ResourceId).HasMaxLength(200);
            builder.Property(x => x.Reason).HasMaxLength(500);
            builder.Property(x => x.SourceIp).HasMaxLength(64);
            builder.Property(x => x.CorrelationId).HasMaxLength(100);
            builder.HasIndex(x => x.OccurredAt);
            builder.HasIndex(x => new { x.ActorUserId, x.OccurredAt });
        });
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        GuardAppendOnlyAudit();

        var now = _clock.UtcNow;
        foreach (var entry in ChangeTracker.Entries<AuditableEntity>())
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    entry.Entity.CreatedAt = now;
                    entry.Entity.ConcurrencyStamp = Guid.NewGuid();
                    break;
                case EntityState.Modified:
                    entry.Entity.ModifiedAt = now;
                    entry.Entity.ConcurrencyStamp = Guid.NewGuid();
                    break;
                default:
                    break;
            }
        }

        return base.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Enforces ADR-016 in code, not only by convention: audit history cannot be
    /// rewritten through this context.
    /// </summary>
    private void GuardAppendOnlyAudit()
    {
        foreach (var entry in ChangeTracker.Entries<AuditEvent>())
        {
            if (entry.State is EntityState.Modified or EntityState.Deleted)
            {
                throw new InvalidOperationException(
                    "Audit events are append-only (ADR-016). Record a new event instead of "
                    + $"attempting to {entry.State.ToString().ToLowerInvariant()} an existing one.");
            }
        }
    }

    private static void ConfigureAudit<TEntity>(
        Microsoft.EntityFrameworkCore.Metadata.Builders.EntityTypeBuilder<TEntity> builder)
        where TEntity : AuditableEntity
    {
        builder.Property(x => x.CreatedBy).HasMaxLength(120).IsRequired();
        builder.Property(x => x.ModifiedBy).HasMaxLength(120);
        builder.Property(x => x.ConcurrencyStamp).IsConcurrencyToken();
    }
}
