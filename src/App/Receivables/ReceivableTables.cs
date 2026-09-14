// Copyright (c) 2026 The DealerFOSS contributors.
// SPDX-License-Identifier: AGPL-3.0-or-later
//
// Overview: Purpose, File Design, and Engineering
//   ReceivableTables — how what customers owe, and what they have paid, are
//   stored.
//
// Usage:
//   Nothing calls these directly. TenantDb finds them by scanning the assembly.
//
// Coding Instructions:
//   THE (Source, Reference) INDEX IS UNIQUE AND THAT IS THE POINT. One car
//   delivered is one debt; one job invoiced is one debt. Billing the same thing
//   twice would show a customer owing double and put the sub-ledger permanently
//   out of step with account 1100, which nothing would notice until somebody
//   chased money that was never owed.
//
//   There is deliberately no stored balance. Outstanding is computed from the
//   payments — see Receivable — so there is no column here for a number that
//   could disagree with the rows underneath it.

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using DealerFOSS.Core;
using DealerFOSS.Data;

namespace DealerFOSS.Receivables;

/// <summary>Receivables have their own schema, like every other capability (ADR-014).</summary>
public static class ReceivableSchema
{
    public const string Name = "receivables";
}

internal sealed class ReceivableConfiguration : IEntityTypeConfiguration<Receivable>
{
    public void Configure(EntityTypeBuilder<Receivable> builder)
    {
        builder.ToTable("Receivables", ReceivableSchema.Name);
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();

        builder.Property(x => x.RooftopId)
            .HasConversion(id => id.Value, value => new RooftopId(value))
            .IsRequired();

        builder.Property(x => x.Reference).HasMaxLength(100).IsRequired();
        builder.Property(x => x.Currency).HasMaxLength(3).IsRequired();
        builder.Property(x => x.Amount).HasPrecision(18, 2);

        builder.Property(x => x.Source).HasConversion<string>().HasMaxLength(20).IsRequired();

        // One billed thing, one debt. See this file's header.
        builder.HasIndex(x => new { x.Source, x.Reference }).IsUnique();

        // What a dealership actually asks: who at this lot still owes us, oldest
        // first. Both parts of that question are in this index.
        builder.HasIndex(x => new { x.RooftopId, x.BilledAt });
        builder.HasIndex(x => x.CustomerId);

        builder.HasMany(x => x.Payments)
            .WithOne()
            .HasForeignKey(p => p.ReceivableId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(x => x.Payments).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.Ignore(x => x.Paid);
        builder.Ignore(x => x.Outstanding);
        builder.Ignore(x => x.IsSettled);

        builder.ConfigureAudit();
    }
}

internal sealed class PaymentConfiguration : IEntityTypeConfiguration<Payment>
{
    public void Configure(EntityTypeBuilder<Payment> builder)
    {
        builder.ToTable("Payments", ReceivableSchema.Name);
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();

        builder.Property(x => x.Amount).HasPrecision(18, 2);
        builder.Property(x => x.Currency).HasMaxLength(3).IsRequired();
        builder.Property(x => x.Method).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(x => x.Note).HasMaxLength(200);

        builder.HasIndex(x => new { x.ReceivableId, x.ReceivedAt });

        builder.ConfigureAudit();
    }
}

internal sealed class CustomerCreditConfiguration : IEntityTypeConfiguration<CustomerCredit>
{
    public void Configure(EntityTypeBuilder<CustomerCredit> builder)
    {
        builder.ToTable("CustomerCredits", ReceivableSchema.Name);
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();

        builder.Property(x => x.RooftopId)
            .HasConversion(id => id.Value, value => new RooftopId(value))
            .IsRequired();

        builder.Property(x => x.Reference).HasMaxLength(100).IsRequired();
        builder.Property(x => x.Currency).HasMaxLength(3).IsRequired();
        builder.Property(x => x.Amount).HasPrecision(18, 2);

        // Deliberately NOT unique on (Reference), unlike Receivables. One bill is
        // one debt, but a customer can overpay the same bill twice — a deposit
        // over the odds, then the balance over the odds again — and each is its
        // own credit with its own provenance.
        builder.HasIndex(x => new { x.CustomerId, x.RaisedAt });
        builder.HasIndex(x => new { x.RooftopId, x.RaisedAt });

        builder.HasMany(x => x.Uses)
            .WithOne()
            .HasForeignKey(u => u.CustomerCreditId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(x => x.Uses).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.Ignore(x => x.Spent);
        builder.Ignore(x => x.Remaining);
        builder.Ignore(x => x.IsSpent);

        builder.ConfigureAudit();
    }
}

internal sealed class CreditUseConfiguration : IEntityTypeConfiguration<CreditUse>
{
    public void Configure(EntityTypeBuilder<CreditUse> builder)
    {
        builder.ToTable("CreditUses", ReceivableSchema.Name);
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();

        builder.Property(x => x.Amount).HasPrecision(18, 2);
        builder.Property(x => x.Currency).HasMaxLength(3).IsRequired();
        builder.Property(x => x.Kind).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(x => x.Note).HasMaxLength(200);

        builder.HasIndex(x => new { x.CustomerCreditId, x.UsedAt });

        // What settled a given bill from a credit rather than from money in.
        builder.HasIndex(x => x.ReceivableId);

        builder.ConfigureAudit();
    }
}
