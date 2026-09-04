// Copyright (c) 2026 The DealerFOSS contributors.
// SPDX-License-Identifier: AGPL-3.0-or-later
//
// Overview: Purpose, File Design, and Engineering
//   LedgerTests — the two rules that make a ledger trustworthy: it balances, and
//   it is never rewritten.
//
// Usage:
//   Runs with the normal test suite; no infrastructure required.
//
// Coding Instructions:
//   If you find yourself relaxing one of these to make something else pass,
//   the something else is wrong. An entry that does not balance is not a
//   record of anything.

using FluentAssertions;
using DealerFOSS.Accounting;
using DealerFOSS.Core;

namespace DealerFOSS.UnitTests;

public sealed class LedgerTests
{
    private static readonly Guid CashId = Guid.NewGuid();
    private static readonly Guid RevenueId = Guid.NewGuid();

    [Fact]
    public void A_balanced_entry_posts()
    {
        var entry = Post([("1000", CashId, 1000m, 0m), ("4000", RevenueId, 0m, 1000m)]);

        entry.TotalDebits.Amount.Should().Be(1000m);
        entry.TotalCredits.Amount.Should().Be(1000m);
        entry.Lines.Should().HaveCount(2);
    }

    [Fact]
    public void An_entry_that_does_not_balance_is_refused_with_the_difference()
    {
        var lopsided = () => Post([("1000", CashId, 1000m, 0m), ("4000", RevenueId, 0m, 900m)]);

        lopsided.Should().Throw<ArgumentException>()
            .WithMessage("*100.00*", because: "the message should say how far out it is");
    }

    [Fact]
    public void An_entry_needs_at_least_two_sides()
    {
        var lonely = () => Post([("1000", CashId, 1000m, 0m)]);

        lonely.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void An_entry_of_zero_records_nothing()
    {
        var empty = () => Post([("1000", CashId, 0m, 0m), ("4000", RevenueId, 0m, 0m)]);

        empty.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void A_line_is_a_debit_or_a_credit_but_not_both()
    {
        var both = () => Post([("1000", CashId, 500m, 500m), ("4000", RevenueId, 0m, 500m)]);

        both.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void A_negative_line_is_refused_because_the_other_side_exists()
    {
        var negative = () => Post([("1000", CashId, -100m, 0m), ("4000", RevenueId, 0m, -100m)]);

        negative.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void An_entry_needs_a_reference_to_what_caused_it()
    {
        var anonymous = () => JournalEntry.Post(
            Guid.NewGuid(), LegalEntityId.New(), RooftopId.New(),
            DateOnly.FromDateTime(DateTime.UtcNow), JournalSource.DealDelivery,
            reference: "  ", memo: "", currency: "USD",
            lines: [("1000", CashId, 1000m, 0m, null), ("4000", RevenueId, 0m, 1000m, null)],
            postedAt: DateTimeOffset.UtcNow);

        anonymous.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void A_reversal_swaps_every_side_and_points_at_the_original()
    {
        var original = Post([("1000", CashId, 1000m, 0m), ("4000", RevenueId, 0m, 1000m)]);

        var reversal = original.BuildReversal(
            Guid.NewGuid(), DateOnly.FromDateTime(DateTime.UtcNow), DateTimeOffset.UtcNow,
            Guid.NewGuid(), "Deal unwound.");

        reversal.ReversesEntryId.Should().Be(original.Id);
        reversal.Source.Should().Be(JournalSource.Reversal);
        reversal.Memo.Should().Be("Deal unwound.");
        reversal.Lines.Single(l => l.AccountCode == "1000").Credit.Should().Be(1000m);
        reversal.Lines.Single(l => l.AccountCode == "4000").Debit.Should().Be(1000m);

        // And it still balances, which is the point of doing it this way.
        reversal.TotalDebits.Amount.Should().Be(reversal.TotalCredits.Amount);
    }

    [Fact]
    public void The_original_is_untouched_by_its_reversal()
    {
        var original = Post([("1000", CashId, 1000m, 0m), ("4000", RevenueId, 0m, 1000m)]);

        original.BuildReversal(
            Guid.NewGuid(), DateOnly.FromDateTime(DateTime.UtcNow), DateTimeOffset.UtcNow, null, "Wrong car.");

        original.Lines.Single(l => l.AccountCode == "1000").Debit.Should().Be(1000m,
            because: "an auditor must be able to see what was originally recorded");
        original.ReversesEntryId.Should().BeNull();
    }

    [Fact]
    public void A_reversal_needs_a_reason()
    {
        var original = Post([("1000", CashId, 1000m, 0m), ("4000", RevenueId, 0m, 1000m)]);

        var unexplained = () => original.BuildReversal(
            Guid.NewGuid(), DateOnly.FromDateTime(DateTime.UtcNow), DateTimeOffset.UtcNow, null, "  ");

        unexplained.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Assets_and_expenses_increase_on_the_debit_side()
    {
        new Account(Guid.NewGuid(), "1000", "Cash", AccountKind.Asset).IncreasesOnDebit.Should().BeTrue();
        new Account(Guid.NewGuid(), "5000", "Cost of sales", AccountKind.Expense).IncreasesOnDebit.Should().BeTrue();
        new Account(Guid.NewGuid(), "4000", "Sales", AccountKind.Revenue).IncreasesOnDebit.Should().BeFalse();
        new Account(Guid.NewGuid(), "2000", "Payables", AccountKind.Liability).IncreasesOnDebit.Should().BeFalse();
    }

    private static JournalEntry Post((string Code, Guid Id, decimal Debit, decimal Credit)[] lines) =>
        JournalEntry.Post(
            Guid.NewGuid(),
            LegalEntityId.New(),
            RooftopId.New(),
            DateOnly.FromDateTime(DateTime.UtcNow),
            JournalSource.DealDelivery,
            reference: "DEAL-1",
            memo: "A sale",
            currency: "USD",
            lines: lines.Select(l => (l.Code, l.Id, l.Debit, l.Credit, (string?)null)),
            postedAt: DateTimeOffset.UtcNow);
}
