// Copyright (c) 2026 The DealerFOSS contributors.
// SPDX-License-Identifier: AGPL-3.0-or-later
//
// Overview: Purpose, File Design, and Engineering
//   JournalEntry — one balanced accounting event, posted once and never changed.
//
// Usage:
//   JournalEntry.Post(...). It refuses to exist unless it balances.
//
// Coding Instructions:
//   This is the strictest thing in the codebase and it should stay that way
//   (ADR-016). A posted entry is never edited and never deleted — a mistake
//   is corrected by posting its reversal, which leaves both the error and
//   the correction visible. That is not bureaucracy: it is the only way an
//   auditor can tell the difference between "this was always right" and
//   "somebody changed it afterwards".

using DealerFOSS.Core;

namespace DealerFOSS.Accounting;

public sealed class JournalEntry : IAppendOnly
{
    private readonly List<JournalLine> _lines = [];

    public Guid Id { get; private set; }

    /// <summary>The legal entity the money belongs to. Not the rooftop.</summary>
    public LegalEntityId LegalEntityId { get; private set; }

    /// <summary>The rooftop that generated it, for departmental reporting.</summary>
    public RooftopId RooftopId { get; private set; }

    public DateOnly EntryDate { get; private set; }

    public JournalSource Source { get; private set; }

    /// <summary>What caused it — a deal id, an invoice number.</summary>
    public string Reference { get; private set; } = string.Empty;

    public string Memo { get; private set; } = string.Empty;

    public string Currency { get; private set; } = string.Empty;

    public DateTimeOffset PostedAt { get; private set; }

    public Guid? PostedByUserId { get; private set; }

    /// <summary>Set when this entry exists to undo another one.</summary>
    public Guid? ReversesEntryId { get; private set; }

    public IReadOnlyList<JournalLine> Lines => _lines;

    public Money TotalDebits => new(_lines.Sum(l => l.Debit), Currency);

    public Money TotalCredits => new(_lines.Sum(l => l.Credit), Currency);

    private JournalEntry()
    {
    }

    /// <summary>
    /// Posts a balanced entry. An unbalanced one is refused here rather than
    /// discovered at month end, because by then nobody remembers what it was for.
    /// </summary>
    public static JournalEntry Post(
        Guid id,
        LegalEntityId legalEntityId,
        RooftopId rooftopId,
        DateOnly entryDate,
        JournalSource source,
        string reference,
        string memo,
        string currency,
        IEnumerable<(string AccountCode, Guid AccountId, decimal Debit, decimal Credit, string? Memo)> lines,
        DateTimeOffset postedAt,
        Guid? postedByUserId = null,
        Guid? reversesEntryId = null)
    {
        ArgumentNullException.ThrowIfNull(lines);

        if (string.IsNullOrWhiteSpace(reference))
        {
            throw new ArgumentException("An entry needs a reference to what caused it.", nameof(reference));
        }

        // Constructing a Money proves the currency is a real ISO code.
        var zero = Money.Zero(currency);

        var entry = new JournalEntry
        {
            Id = id,
            LegalEntityId = legalEntityId,
            RooftopId = rooftopId,
            EntryDate = entryDate,
            Source = source,
            Reference = reference.Trim(),
            Memo = (memo ?? string.Empty).Trim(),
            Currency = zero.Currency,
            PostedAt = postedAt,
            PostedByUserId = postedByUserId,
            ReversesEntryId = reversesEntryId,
        };

        foreach (var line in lines)
        {
            entry._lines.Add(new JournalLine(
                Guid.NewGuid(), id, line.AccountId, line.AccountCode, line.Debit, line.Credit, line.Memo));
        }

        if (entry._lines.Count < 2)
        {
            throw new ArgumentException(
                "An entry needs at least two lines — something has to balance against something.",
                nameof(lines));
        }

        var debits = entry._lines.Sum(l => l.Debit);
        var credits = entry._lines.Sum(l => l.Credit);

        if (debits != credits)
        {
            throw new ArgumentException(
                $"An entry must balance. Debits are {debits:0.00} and credits are {credits:0.00}, "
                + $"a difference of {Math.Abs(debits - credits):0.00}.",
                nameof(lines));
        }

        if (debits == 0m)
        {
            throw new ArgumentException("An entry of zero has nothing to record.", nameof(lines));
        }

        return entry;
    }

    /// <summary>
    /// Builds the entry that undoes this one: the same lines with debits and
    /// credits swapped. The original is left exactly as it was.
    /// </summary>
    public JournalEntry BuildReversal(Guid id, DateOnly entryDate, DateTimeOffset postedAt, Guid? postedByUserId, string reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new ArgumentException(
                "A reversal needs a reason. An unexplained one is indistinguishable from a mistake.",
                nameof(reason));
        }

        return Post(
            id,
            LegalEntityId,
            RooftopId,
            entryDate,
            JournalSource.Reversal,
            Reference,
            reason.Trim(),
            Currency,
            _lines.Select(l => (l.AccountCode, l.AccountId, l.Credit, l.Debit, l.Memo)),
            postedAt,
            postedByUserId,
            reversesEntryId: Id);
    }
}

public enum JournalSource
{
    /// <summary>A car left the lot.</summary>
    DealDelivery = 0,

    /// <summary>Undoing an earlier entry.</summary>
    Reversal = 1,

    /// <summary>A repair order was invoiced.</summary>
    ServiceInvoice = 3,

    /// <summary>Entered by hand. Not yet possible.</summary>
    Manual = 2,
}
