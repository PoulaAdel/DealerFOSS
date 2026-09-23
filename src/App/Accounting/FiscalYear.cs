// Copyright (c) 2026 The DealerFOSS contributors.
// SPDX-License-Identifier: AGPL-3.0-or-later
//
// Overview: Purpose, File Design, and Engineering
//   FiscalYear — whether a calendar year is still open to postings, and the
//   closing entry that zeroed it if it is not.
//
//   The sibling of AccountingPeriod, one level up: a month decides whether a
//   day's work may be posted at all, and a year decides whether a whole
//   twelve months of it is finished and carried forward. Both are gates on
//   the same PeriodRefusalAsync check — see AccountingService — because a
//   reopened month inside a still-closed year must still be refused, or the
//   two gates would not actually agree with each other.
//
// Usage:
//   FiscalYear.Open(...) the first time a year is touched; Close it once
//   every month in it is closed; Reopen unlocks one that was closed.
//
// Coding Instructions:
//   CLOSING POSTS AN ENTRY AND DOES NOT INVENT A SECOND WAY TO MOVE MONEY.
//   AccountingService.CloseYearAsync computes what every revenue and expense
//   account carried for this year and posts one ordinary JournalEntry through
//   the same JournalEntry.Post every other posting uses — zeroing each of
//   them and carrying the net to RetainedEarnings. This entity only tracks
//   whether that has happened; it holds no arithmetic of its own, the same
//   division of labour AccountingPeriod already has with the entries dated
//   into it.
//
//   REOPENING DOES NOT UNDO THE CLOSING ENTRY. It only permits new postings
//   again, exactly like reopening a month. Undoing the entry itself is
//   ReverseAsync on that entry's own id — a closing entry is a JournalEntry
//   like any other, and inventing a second undo path for this one would be
//   the same mistake the month-end work already refused to make.

using DealerFOSS.Core;

namespace DealerFOSS.Accounting;

public enum FiscalYearState
{
    /// <summary>Accepting postings — or would be, if every month in it is open too.</summary>
    Open = 0,

    /// <summary>Closed. Its closing entry has been posted and nothing may date into it.</summary>
    Closed = 1,
}

/// <summary>
/// One calendar year of the organization's books. Organization-wide, the same
/// reason AccountingPeriod is: a tenant database holds one set of books.
/// </summary>
public sealed class FiscalYear : AuditableEntity
{
    private readonly List<FiscalYearChange> _history = [];

    public Guid Id { get; private set; }

    public int Year { get; private set; }

    public FiscalYearState State { get; private set; }

    public DateTimeOffset? ClosedAt { get; private set; }

    public Guid? ClosedByUserId { get; private set; }

    /// <summary>
    /// The closing entry, once posted. Null on an open year, and still set
    /// after a reopen — the entry is not undone by reopening, see the file
    /// header, so the last close this year had is worth keeping a pointer to.
    /// </summary>
    public Guid? ClosingEntryId { get; private set; }

    public IReadOnlyList<FiscalYearChange> History => _history;

    public DateOnly StartsOn => new(Year, 1, 1);

    public DateOnly EndsOn => new(Year, 12, 31);

    private FiscalYear()
    {
    }

    public static FiscalYear Open(Guid id, int year, DateTimeOffset at, Guid? byUserId, string? note)
    {
        if (year is < 2000 or > 2999)
        {
            throw new ArgumentOutOfRangeException(
                nameof(year), "That year is almost certainly a typo rather than a decision.");
        }

        var fiscalYear = new FiscalYear
        {
            Id = id,
            Year = year,
            State = FiscalYearState.Open,
        };

        fiscalYear._history.Add(new FiscalYearChange(
            Guid.NewGuid(), id, null, FiscalYearState.Open, at, byUserId, note));

        return fiscalYear;
    }

    /// <summary>
    /// Locks the year, once its closing entry has been posted. Called by
    /// AccountingService.CloseYearAsync inside the same transaction as that
    /// posting — the state and the entry that justifies it commit together.
    /// </summary>
    public void Close(Guid closingEntryId, DateTimeOffset at, Guid? byUserId, string? note)
    {
        if (State == FiscalYearState.Closed)
        {
            throw new InvalidOperationException($"{Year} is already closed.");
        }

        State = FiscalYearState.Closed;
        ClosedAt = at;
        ClosedByUserId = byUserId;
        ClosingEntryId = closingEntryId;

        _history.Add(new FiscalYearChange(
            Guid.NewGuid(), Id, FiscalYearState.Open, FiscalYearState.Closed, at, byUserId, note));
    }

    /// <summary>
    /// Unlocks a closed year so something that genuinely belongs in it can be
    /// posted. Needs a reason, the same discipline reopening a month already
    /// has — a year that was reported closed and then reopened is exactly the
    /// sequence somebody will later need to reconstruct.
    /// </summary>
    public void Reopen(DateTimeOffset at, Guid? byUserId, string reason)
    {
        if (State == FiscalYearState.Open)
        {
            throw new InvalidOperationException($"{Year} is already open.");
        }

        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new ArgumentException("Reopening a closed year needs a reason on the record.", nameof(reason));
        }

        State = FiscalYearState.Open;
        ClosedAt = null;
        ClosedByUserId = null;

        _history.Add(new FiscalYearChange(
            Guid.NewGuid(), Id, FiscalYearState.Closed, FiscalYearState.Open, at, byUserId, reason.Trim()));
    }
}

/// <summary>
/// One transition of a fiscal year. Append-only, the same reason
/// AccountingPeriodChange is.
/// </summary>
public sealed class FiscalYearChange : IAppendOnly
{
    public Guid Id { get; private set; }

    public Guid FiscalYearId { get; private set; }

    public FiscalYearState? FromState { get; private set; }

    public FiscalYearState ToState { get; private set; }

    public DateTimeOffset OccurredAt { get; private set; }

    /// <summary>Insertion order, assigned by the database. See AccountingPeriodChange.Sequence.</summary>
    public long Sequence { get; private set; }

    public Guid? ChangedByUserId { get; private set; }

    public string? Note { get; private set; }

    private FiscalYearChange()
    {
    }

    internal FiscalYearChange(
        Guid id,
        Guid fiscalYearId,
        FiscalYearState? fromState,
        FiscalYearState toState,
        DateTimeOffset occurredAt,
        Guid? changedByUserId,
        string? note)
    {
        Id = id;
        FiscalYearId = fiscalYearId;
        FromState = fromState;
        ToState = toState;
        OccurredAt = occurredAt;
        ChangedByUserId = changedByUserId;
        Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
    }
}
