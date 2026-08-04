// OrganizationTables — how this feature's records are stored. Owns the "org"
// schema and no other (ADR-014).
//
// Use:  nothing calls these directly. TenantDb finds them by scanning the
//       assembly, so adding a configuration here is all that is needed.
// Edit: typed ids need a HasConversion or they will not persist as GUIDs. Audit
//       columns and the concurrency token are stamped centrally by TenantDb on
//       save — do not set them at call sites.

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using DealerFOSS.Core;
using DealerFOSS.Data;

namespace DealerFOSS.Organization;

/// <summary>The schema this feature owns.</summary>
internal static class OrganizationSchema
{
    public const string Name = "org";
}

internal sealed class DealerOrganizationConfiguration : IEntityTypeConfiguration<DealerOrganization>
{
    public void Configure(EntityTypeBuilder<DealerOrganization> builder)
    {
        builder.ToTable("DealerOrganizations", OrganizationSchema.Name);
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
        builder.ConfigureAudit();
    }
}

internal sealed class LegalEntityConfiguration : IEntityTypeConfiguration<LegalEntity>
{
    public void Configure(EntityTypeBuilder<LegalEntity> builder)
    {
        builder.ToTable("LegalEntities", OrganizationSchema.Name);
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
        builder.ConfigureAudit();
    }
}

internal sealed class RooftopConfiguration : IEntityTypeConfiguration<Rooftop>
{
    public void Configure(EntityTypeBuilder<Rooftop> builder)
    {
        builder.ToTable("Rooftops", OrganizationSchema.Name);
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
        builder.ConfigureAudit();
    }
}

internal sealed class DepartmentConfiguration : IEntityTypeConfiguration<Department>
{
    public void Configure(EntityTypeBuilder<Department> builder)
    {
        builder.ToTable("Departments", OrganizationSchema.Name);
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id)
            .HasConversion(id => id.Value, value => new DepartmentId(value))
            .ValueGeneratedNever();
        builder.Property(x => x.RooftopId)
            .HasConversion(id => id.Value, value => new RooftopId(value));
        builder.Property(x => x.Name).HasMaxLength(120).IsRequired();
        builder.Property(x => x.Kind).HasConversion<string>().HasMaxLength(30);
        builder.ConfigureAudit();
    }
}
