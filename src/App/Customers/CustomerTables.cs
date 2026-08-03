// CustomerTables — how this feature's records are stored. Owns the "customers"
// schema and no other (ADR-014).
//
// Use:  nothing calls these directly. TenantDb finds them by scanning the
//       assembly.
// Edit: there is deliberately no unique index on a contact value. Two customers
//       can share an email or a phone — a couple, a family business — and
//       enforcing uniqueness would block legitimate records and push staff into
//       creating bad data to get around it (doc 04 §4).

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OpenDealer360.Core;
using OpenDealer360.Data;

namespace OpenDealer360.Customers;

/// <summary>The schema this feature owns.</summary>
internal static class CustomerSchema
{
    public const string Name = "customers";
}

internal sealed class CustomerConfiguration : IEntityTypeConfiguration<Customer>
{
    public void Configure(EntityTypeBuilder<Customer> builder)
    {
        builder.ToTable("Customers", CustomerSchema.Name);
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.Kind).HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.FirstName).HasMaxLength(120).IsRequired();
        builder.Property(x => x.LastName).HasMaxLength(200).IsRequired();
        builder.Property(x => x.HomeRooftopId)
            .HasConversion(id => id!.Value.Value, value => new RooftopId(value));
        builder.Property(x => x.ExternalReference).HasMaxLength(200);

        builder.Ignore(x => x.DisplayName);

        // Filtered, so the uniqueness applies only to imported customers. A plain
        // unique index would allow exactly one hand-typed customer per database,
        // because every one of them has a null here.
        builder.HasIndex(x => x.ExternalReference)
            .IsUnique()
            .HasFilter("[ExternalReference] IS NOT NULL");

        // Search hits these two constantly; archived customers are filtered
        // out of every list, so it belongs in the index rather than beside it.
        builder.HasIndex(x => new { x.IsArchived, x.LastName });
        builder.HasIndex(x => x.HomeRooftopId);

        builder.OwnsOne(x => x.Address, address =>
        {
            address.Property(a => a.Line1).HasColumnName("AddressLine1").HasMaxLength(200);
            address.Property(a => a.Line2).HasColumnName("AddressLine2").HasMaxLength(200);
            address.Property(a => a.City).HasColumnName("City").HasMaxLength(120);
            address.Property(a => a.AdministrativeArea).HasColumnName("AdministrativeArea").HasMaxLength(120);
            address.Property(a => a.PostalCode).HasColumnName("PostalCode").HasMaxLength(20);
            address.Property(a => a.Country).HasColumnName("Country").HasMaxLength(2);
        });

        builder.HasMany(x => x.ContactPoints)
            .WithOne()
            .HasForeignKey(c => c.CustomerId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(x => x.ContactPoints).AutoInclude();

        builder.ConfigureAudit();
    }
}

internal sealed class ContactPointConfiguration : IEntityTypeConfiguration<ContactPoint>
{
    public void Configure(EntityTypeBuilder<ContactPoint> builder)
    {
        builder.ToTable("ContactPoints", CustomerSchema.Name);
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.Kind).HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.Value).HasMaxLength(320).IsRequired();
        // Searching by email or phone looks the value up directly — but not
        // uniquely; see the note at the top of this file.
        builder.HasIndex(x => x.Value);
        builder.HasIndex(x => new { x.CustomerId, x.Kind });
    }
}
