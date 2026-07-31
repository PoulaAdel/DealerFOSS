// JournalLine — one side of one account movement.
//
// Use:  created through JournalEntry.Post; never on its own.
// Edit: a line is a debit OR a credit, never both and never neither. Allowing
//       both would let an entry balance itself line by line and hide what
//       actually moved. The account code is stored alongside the id on purpose:
//       a printed journal from three years ago must still be readable if the
//       chart is later renumbered.

using OpenDealer360.Core;

namespace OpenDealer360.Accounting;

public sealed class JournalLine : IAppendOnly
{
    public Guid Id { get; private set; }

    public Guid EntryId { get; private set; }

    public Guid AccountId { get; private set; }

    /// <summary>The code as it was when this was posted.</summary>
    public string AccountCode { get; private set; } = string.Empty;

    public decimal Debit { get; private set; }

    public decimal Credit { get; private set; }

    public string? Memo { get; private set; }

    private JournalLine()
    {
    }

    internal JournalLine(
        Guid id,
        Guid entryId,
        Guid accountId,
        string accountCode,
        decimal debit,
        decimal credit,
        string? memo)
    {
        if (debit < 0 || credit < 0)
        {
            throw new ArgumentException(
                "A line is never negative. Put the amount on the other side instead.", nameof(debit));
        }

        if (debit > 0 && credit > 0)
        {
            throw new ArgumentException(
                "A line is a debit or a credit, not both.", nameof(debit));
        }

        if (debit == 0 && credit == 0)
        {
            throw new ArgumentException("A line of zero records nothing.", nameof(debit));
        }

        Id = id;
        EntryId = entryId;
        AccountId = accountId;
        AccountCode = accountCode;
        Debit = debit;
        Credit = credit;
        Memo = string.IsNullOrWhiteSpace(memo) ? null : memo.Trim();
    }
}
