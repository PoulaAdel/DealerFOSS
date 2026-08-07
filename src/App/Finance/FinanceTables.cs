// FinanceTables — how the F&I product catalogue is stored.
//
// Use:  nothing calls these directly. TenantDb finds them by scanning the
//       assembly.
// Edit: the name is unique across the organization, which is what stops two
//       versions of the same warranty appearing. Note there is no foreign key
//       from a sold DealProduct to here — see DealTables for why.

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using DealerFOSS.Data;

namespace DealerFOSS.Finance;

/// <summary>The schema this capability owns (ADR-014).</summary>
internal static class FinanceSchema
{
    public const string Name = "finance";
}

internal sealed class FinanceProductConfiguration : IEntityTypeConfiguration<FinanceProduct>
{
    public void Configure(EntityTypeBuilder<FinanceProduct> builder)
    {
        builder.ToTable("FinanceProducts", FinanceSchema.Name);
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.Name).HasMaxLength(200).IsRequired();
        builder.Property(x => x.Kind).HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.Provider).HasMaxLength(200).IsRequired();
        builder.Property(x => x.DefaultPrice).HasPrecision(18, 2);
        builder.Property(x => x.DefaultCost).HasPrecision(18, 2);
        builder.Property(x => x.Currency).HasMaxLength(3);

        builder.HasIndex(x => x.Name).IsUnique();

        // The menu a salesperson sees: what can be sold, grouped by kind.
        builder.HasIndex(x => new { x.IsAvailable, x.Kind });

        builder.ConfigureAudit();
    }
}
