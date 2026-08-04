// AccountingTables — how the ledger is stored. Owns the "accounting" schema and
// no other (ADR-014).
//
// Use:  nothing calls these directly. TenantDb finds them by scanning the
//       assembly.
// Edit: journal entries and lines carry no audit columns and no concurrency
//       token, unlike every other table here — because they are never updated.
//       They are marked IAppendOnly, which TenantDb enforces. If you find
//       yourself wanting a "modified" column on a journal line, what you actually
//       want is a reversal.

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using DealerFOSS.Core;
using DealerFOSS.Data;

namespace DealerFOSS.Accounting;

/// <summary>The schema this capability owns.</summary>
internal static class AccountingSchema
{
    public const string Name = "accounting";
}

internal sealed class AccountConfiguration : IEntityTypeConfiguration<Account>
{
    public void Configure(EntityTypeBuilder<Account> builder)
    {
        builder.ToTable("Accounts", AccountingSchema.Name);
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.Code).HasMaxLength(20).IsRequired();
        builder.Property(x => x.Name).HasMaxLength(120).IsRequired();
        builder.Property(x => x.Kind).HasConversion<string>().HasMaxLength(20);

        builder.Ignore(x => x.IncreasesOnDebit);

        // A code identifies one account or none.
        builder.HasIndex(x => x.Code).IsUnique();

        builder.ConfigureAudit();
    }
}

internal sealed class JournalEntryConfiguration : IEntityTypeConfiguration<JournalEntry>
{
    public void Configure(EntityTypeBuilder<JournalEntry> builder)
    {
        builder.ToTable("JournalEntries", AccountingSchema.Name);
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.LegalEntityId)
            .HasConversion(id => id.Value, value => new LegalEntityId(value))
            .IsRequired();
        builder.Property(x => x.RooftopId)
            .HasConversion(id => id.Value, value => new RooftopId(value))
            .IsRequired();
        builder.Property(x => x.Source).HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.Reference).HasMaxLength(100).IsRequired();
        builder.Property(x => x.Memo).HasMaxLength(500);
        builder.Property(x => x.Currency).HasMaxLength(3).IsRequired();

        builder.Ignore(x => x.TotalDebits);
        builder.Ignore(x => x.TotalCredits);

        // Every ledger question is "what happened at this location, in this
        // period" or "what did this deal do".
        builder.HasIndex(x => new { x.RooftopId, x.EntryDate });
        builder.HasIndex(x => new { x.Reference, x.Source });
        builder.HasIndex(x => x.ReversesEntryId);

        builder.HasMany(x => x.Lines)
            .WithOne()
            .HasForeignKey(l => l.EntryId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(x => x.Lines).AutoInclude();
    }
}

internal sealed class JournalLineConfiguration : IEntityTypeConfiguration<JournalLine>
{
    public void Configure(EntityTypeBuilder<JournalLine> builder)
    {
        builder.ToTable("JournalLines", AccountingSchema.Name);
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.AccountCode).HasMaxLength(20).IsRequired();
        builder.Property(x => x.Debit).HasPrecision(18, 2);
        builder.Property(x => x.Credit).HasPrecision(18, 2);
        builder.Property(x => x.Memo).HasMaxLength(200);

        builder.HasIndex(x => x.EntryId);
        builder.HasIndex(x => x.AccountId);
    }
}
