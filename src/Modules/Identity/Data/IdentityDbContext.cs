using Microsoft.EntityFrameworkCore;
using OpenDealer360.Identity.Domain;
using OpenDealer360.Core;

namespace OpenDealer360.Identity.Data;

/// <summary>
/// Persistence for the Identity module. Owns the <c>identity</c> schema and no
/// other (ADR-014). Audit events are append-only: this context refuses to update
/// or delete them (ADR-016).
/// </summary>
public sealed class IdentityDbContext(DbContextOptions<IdentityDbContext> options, IClock clock)
    : DbContext(options)
{
    public const string Schema = "identity";

    private readonly IClock _clock = clock;

    public DbSet<User> Users => Set<User>();

    public DbSet<Role> Roles => Set<Role>();

    public DbSet<UserAssignment> UserAssignments => Set<UserAssignment>();

    public DbSet<AuditEvent> AuditEvents => Set<AuditEvent>();

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
            builder.HasMany(x => x.Assignments)
                .WithOne()
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);
            ConfigureAudit(builder);
        });

        modelBuilder.Entity<Role>(builder =>
        {
            builder.ToTable("Roles");
            builder.HasKey(x => x.Id);
            builder.Property(x => x.Id).ValueGeneratedNever();
            builder.Property(x => x.Name).HasMaxLength(100).IsRequired();
            builder.HasIndex(x => x.Name).IsUnique();
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
