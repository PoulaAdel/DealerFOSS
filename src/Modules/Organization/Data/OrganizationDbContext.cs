using Microsoft.EntityFrameworkCore;
using OpenDealer360.Organization.Domain;
using OpenDealer360.Core;

namespace OpenDealer360.Organization.Data;

/// <summary>
/// Persistence for the Organization module. Owns the <c>org</c> schema and no
/// other — schema ownership is a tested boundary (doc 03 §5, ADR-014). Audit
/// columns and the concurrency stamp are set centrally on save (doc 08 §5).
/// </summary>
public sealed class OrganizationDbContext(DbContextOptions<OrganizationDbContext> options, IClock clock)
    : DbContext(options)
{
    public const string Schema = "org";

    private readonly IClock _clock = clock;

    public DbSet<DealerOrganization> Organizations => Set<DealerOrganization>();

    public DbSet<LegalEntity> LegalEntities => Set<LegalEntity>();

    public DbSet<Rooftop> Rooftops => Set<Rooftop>();

    public DbSet<Department> Departments => Set<Department>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);

        modelBuilder.Entity<DealerOrganization>(builder =>
        {
            builder.ToTable("DealerOrganizations");
            builder.HasKey(x => x.Id);
            builder.Property(x => x.Id)
                .HasConversion(id => id.Value, value => new DealerOrganizationId(value))
                .ValueGeneratedNever();
            builder.Property(x => x.Name).HasMaxLength(200).IsRequired();
            builder.Property(x => x.Slug).HasMaxLength(100).IsRequired();
            builder.HasIndex(x => x.Slug).IsUnique();
            builder.HasMany(x => x.LegalEntities)
                .WithOne()
                .HasForeignKey(x => x.OrganizationId)
                .OnDelete(DeleteBehavior.Cascade);
            ConfigureAudit(builder);
        });

        modelBuilder.Entity<LegalEntity>(builder =>
        {
            builder.ToTable("LegalEntities");
            builder.HasKey(x => x.Id);
            builder.Property(x => x.Id)
                .HasConversion(id => id.Value, value => new LegalEntityId(value))
                .ValueGeneratedNever();
            builder.Property(x => x.OrganizationId)
                .HasConversion(id => id.Value, value => new DealerOrganizationId(value));
            builder.Property(x => x.Name).HasMaxLength(200).IsRequired();
            builder.Property(x => x.RegisteredName).HasMaxLength(200);
            builder.Property(x => x.TaxId).HasMaxLength(50);
            builder.HasMany(x => x.Rooftops)
                .WithOne()
                .HasForeignKey(x => x.LegalEntityId)
                .OnDelete(DeleteBehavior.Cascade);
            ConfigureAudit(builder);
        });

        modelBuilder.Entity<Rooftop>(builder =>
        {
            builder.ToTable("Rooftops");
            builder.HasKey(x => x.Id);
            builder.Property(x => x.Id)
                .HasConversion(id => id.Value, value => new RooftopId(value))
                .ValueGeneratedNever();
            builder.Property(x => x.LegalEntityId)
                .HasConversion(id => id.Value, value => new LegalEntityId(value));
            builder.Property(x => x.Name).HasMaxLength(200).IsRequired();
            builder.Property(x => x.Code).HasMaxLength(30).IsRequired();
            builder.Property(x => x.TimeZone).HasMaxLength(60).IsRequired();
            builder.HasIndex(x => x.Code).IsUnique();
            builder.HasMany(x => x.Departments)
                .WithOne()
                .HasForeignKey(x => x.RooftopId)
                .OnDelete(DeleteBehavior.Cascade);
            ConfigureAudit(builder);
        });

        modelBuilder.Entity<Department>(builder =>
        {
            builder.ToTable("Departments");
            builder.HasKey(x => x.Id);
            builder.Property(x => x.Id)
                .HasConversion(id => id.Value, value => new DepartmentId(value))
                .ValueGeneratedNever();
            builder.Property(x => x.RooftopId)
                .HasConversion(id => id.Value, value => new RooftopId(value));
            builder.Property(x => x.Name).HasMaxLength(120).IsRequired();
            builder.Property(x => x.Kind).HasConversion<string>().HasMaxLength(30);
            ConfigureAudit(builder);
        });
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
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

    private static void ConfigureAudit<TEntity>(
        Microsoft.EntityFrameworkCore.Metadata.Builders.EntityTypeBuilder<TEntity> builder)
        where TEntity : AuditableEntity
    {
        builder.Property(x => x.CreatedBy).HasMaxLength(120).IsRequired();
        builder.Property(x => x.ModifiedBy).HasMaxLength(120);
        builder.Property(x => x.ConcurrencyStamp).IsConcurrencyToken();
    }
}
