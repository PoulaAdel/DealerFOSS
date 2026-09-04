// Copyright (c) 2026 The DealerFOSS contributors.
// SPDX-License-Identifier: AGPL-3.0-or-later
//
// Overview: Purpose, File Design, and Engineering
//   AccountingPeriod — one month of the books, and whether it is still open.
//
// Usage:
//   AccountingPeriod.Open(...) to start a month; Close to lock it; Reopen to
//   unlock one that was closed.
//
// Coding Instructions:
//   The rule this exists to hold is that **locking is an act somebody
//   performs, not a date that passes** (maintainer, 2026-08-06). The cutoff
//   is the calendar month end, but the close then runs over however many
//   business days the work takes — reconciling, adjusting, reviewing — and
//   the month is locked at the end of that. Anything built as "prior-month
//   entries are refused after the Nth" would either lock a month somebody is
//   still working on or leave one open because nobody's calendar said
//   otherwise.
//
//   An adjustment posted DURING the close belongs in the month being closed.
//   That is what the window is for, and it is why Open is the state that
//   accepts postings rather than "the current month".
//
//   Reopening is deliberately not a quiet undo. It needs a written reason and
//   its own permission, and every transition is kept — because a month that
//   was reported on, reopened, and changed is exactly the sequence somebody
//   will later need to reconstruct.

using DealerFOSS.Core;

namespace DealerFOSS.Accounting;

public enum AccountingPeriodState
{
    /// <summary>Accepting postings.</summary>
    Open = 0,

    /// <summary>Locked. Nothing new may be dated into it until it is reopened.</summary>
    Closed = 1,
}

/// <summary>
/// One calendar month of the organization's books. There is exactly one per
/// month, and a tenant database holds one organization, so no rooftop or legal
/// entity appears here — the books close as a whole.
/// </summary>
public sealed class AccountingPeriod : AuditableEntity
{
    private readonly List<AccountingPeriodChange> _history = [];

    public Guid Id { get; private set; }

    public int Year { get; private set; }

    public int Month { get; private set; }

    public AccountingPeriodState State { get; private set; }

    public DateTimeOffset? ClosedAt { get; private set; }

    public Guid? ClosedByUserId { get; private set; }

    public IReadOnlyCollection<AccountingPeriodChange> History => _history;

    /// <summary>The first day of the month this period covers.</summary>
    public DateOnly StartsOn => new(Year, Month, 1);

    /// <summary>
    /// The cutoff: the 30th or the 31st, whichever this month has. Transactions
    /// fall on one side or the other of it.
    /// </summary>
    public DateOnly EndsOn => new(Year, Month, DateTime.DaysInMonth(Year, Month));

    public bool Accepts(DateOnly date) =>
        State == AccountingPeriodState.Open && date >= StartsOn && date <= EndsOn;

    private AccountingPeriod()
    {
    }

    public static AccountingPeriod Open(
        Guid id,
        int year,
        int month,
        DateTimeOffset at,
        Guid? byUserId,
        string? note)
    {
        if (month is < 1 or > 12)
        {
            throw new ArgumentOutOfRangeException(nameof(month), "A month is 1 to 12.");
        }

        if (year is < 2000 or > 2999)
        {
            throw new ArgumentOutOfRangeException(
                nameof(year), "That year is almost certainly a typo rather than a decision.");
        }

        var period = new AccountingPeriod
        {
            Id = id,
            Year = year,
            Month = month,
            State = AccountingPeriodState.Open,
        };

        period._history.Add(new AccountingPeriodChange(
            Guid.NewGuid(), id, null, AccountingPeriodState.Open, at, byUserId, note));

        return period;
    }

    /// <summary>
    /// Locks the month. The close work — reconciling, adjusting, reviewing — has
    /// already happened by the time this is called; this is the act that ends it.
    /// </summary>
    public void Close(DateTimeOffset at, Guid? byUserId, string? note)
    {
        if (State == AccountingPeriodState.Closed)
        {
            throw new InvalidOperationException($"{Year}-{Month:00} is already closed.");
        }

        State = AccountingPeriodState.Closed;
        ClosedAt = at;
        ClosedByUserId = byUserId;

        _history.Add(new AccountingPeriodChange(
            Guid.NewGuid(), Id, AccountingPeriodState.Open, AccountingPeriodState.Closed, at, byUserId, note));
    }

    /// <summary>
    /// Unlocks a closed month so something late can be posted into it. Needs a
    /// reason, because the figure somebody already reported is about to be able
    /// to move.
    /// </summary>
    public void Reopen(DateTimeOffset at, Guid? byUserId, string reason)
    {
        if (State == AccountingPeriodState.Open)
        {
            throw new InvalidOperationException($"{Year}-{Month:00} is already open.");
        }

        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new ArgumentException(
                "Reopening a closed month needs a reason on the record.", nameof(reason));
        }

        State = AccountingPeriodState.Open;
        ClosedAt = null;
        ClosedByUserId = null;

        _history.Add(new AccountingPeriodChange(
            Guid.NewGuid(), Id, AccountingPeriodState.Closed, AccountingPeriodState.Open,
            at, byUserId, reason.Trim()));
    }
}

/// <summary>
/// One transition of a period. Append-only (<see cref="IAppendOnly"/>): a month
/// that was closed, reopened, and changed is exactly the sequence an auditor
/// needs to be able to reconstruct.
/// </summary>
public sealed class AccountingPeriodChange : IAppendOnly
{
    public Guid Id { get; private set; }

    public Guid AccountingPeriodId { get; private set; }

    public AccountingPeriodState? FromState { get; private set; }

    public AccountingPeriodState ToState { get; private set; }

    public DateTimeOffset OccurredAt { get; private set; }

    /// <summary>
    /// Insertion order, assigned by the database. Two entries can carry the same
    /// OccurredAt to the microsecond; ordering on the timestamp alone lets the
    /// store return tied rows in any order, so a history read as a sequence of
    /// events could contradict itself. Never set in code — the column is an IDENTITY.
    /// </summary>
    public long Sequence { get; private set; }

    public Guid? ChangedByUserId { get; private set; }

    public string? Note { get; private set; }

    private AccountingPeriodChange()
    {
    }

    internal AccountingPeriodChange(
        Guid id,
        Guid accountingPeriodId,
        AccountingPeriodState? fromState,
        AccountingPeriodState toState,
        DateTimeOffset occurredAt,
        Guid? changedByUserId,
        string? note)
    {
        Id = id;
        AccountingPeriodId = accountingPeriodId;
        FromState = fromState;
        ToState = toState;
        OccurredAt = occurredAt;
        ChangedByUserId = changedByUserId;
        Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
    }
}
