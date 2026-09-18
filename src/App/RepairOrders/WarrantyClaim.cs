// Copyright (c) 2026 The DealerFOSS contributors.
// SPDX-License-Identifier: AGPL-3.0-or-later
//
// Overview: Purpose, File Design, and Engineering
//   WarrantyClaim — what a repair order billed the manufacturer, and where
//   that stands.
//
//   Invoicing a repair order with warranty-pay lines already debits account
//   1200 (WarrantyReceivable) — see AccountingService.BuildServiceInvoiceLines
//   — but until this landed, that debit had no per-order life after posting:
//   the balance sat in one GL account forever, with no way to say "this
//   claim was submitted last Tuesday" or "the manufacturer knocked $40 off
//   this one." This is the row that gives each claim that life.
//
// Usage:
//   WarrantyClaim.Open(...) inside RepairOrderService's invoicing transaction,
//   when the order has any warranty-pay amount. Moved on by the service
//   through Submit/Approve/Deny/RecordPaid.
//
// Coding Instructions:
//   THIS IS INTERNAL TRACKING, NOT AN OEM INTEGRATION. Nothing here talks to
//   a manufacturer's system — Submitted means a person told this system they
//   sent it, not that a claim portal confirmed receipt. Doc 11 §3 lists real
//   warranty claim submission as blocked on an OEM relationship; that gap is
//   untouched by this file, which only tracks what a service manager already
//   knows by phone and by post.
//
//   PAID CAN DIFFER FROM AMOUNT, ON PURPOSE. A manufacturer that disputes one
//   line on a claim pays less than was billed; forcing AmountPaid to equal
//   Amount would make that impossible to record honestly, and the gap between
//   the two is exactly the number a service manager needs to chase.

using DealerFOSS.Core;

namespace DealerFOSS.RepairOrders;

public enum WarrantyClaimStatus
{
    /// <summary>Invoiced and owed by the manufacturer. Nothing sent yet.</summary>
    Open = 0,

    /// <summary>Sent to the manufacturer, by whatever means this dealership uses.</summary>
    Submitted = 1,

    /// <summary>The manufacturer has agreed to pay it.</summary>
    Approved = 2,

    /// <summary>The manufacturer refused it. Terminal — see the file header.</summary>
    Denied = 3,

    /// <summary>The money arrived. Terminal.</summary>
    Paid = 4,
}

/// <summary>
/// One repair order's warranty-pay work, tracked from invoicing through to
/// whatever the manufacturer eventually does about it.
/// </summary>
public sealed class WarrantyClaim : AuditableEntity
{
    private readonly List<WarrantyClaimStatusChange> _history = [];

    public Guid Id { get; private set; }

    public RooftopId RooftopId { get; private set; }

    /// <summary>One claim per order — see the unique index in WarrantyClaimTables.</summary>
    public Guid RepairOrderId { get; private set; }

    /// <summary>What was billed to the manufacturer. Frozen at invoicing.</summary>
    public decimal Amount { get; private set; }

    public string Currency { get; private set; } = "USD";

    public WarrantyClaimStatus Status { get; private set; }

    /// <summary>
    /// What the manufacturer actually paid. Null until <see cref="Status"/> is
    /// Paid — and then not necessarily equal to <see cref="Amount"/>, see the
    /// file header.
    /// </summary>
    public decimal? AmountPaid { get; private set; }

    public IReadOnlyList<WarrantyClaimStatusChange> History => _history;

    private WarrantyClaim()
    {
    }

    internal static WarrantyClaim Open(
        Guid id,
        RooftopId rooftopId,
        Guid repairOrderId,
        Money amount,
        DateTimeOffset openedAt,
        Guid? byUserId)
    {
        if (amount.Amount <= 0m)
        {
            throw new ArgumentOutOfRangeException(
                nameof(amount), "A warranty claim is for an amount above zero.");
        }

        var claim = new WarrantyClaim
        {
            Id = id,
            RooftopId = rooftopId,
            RepairOrderId = repairOrderId,
            Amount = amount.Amount,
            Currency = amount.Currency,
            Status = WarrantyClaimStatus.Open,
        };

        claim._history.Add(new WarrantyClaimStatusChange(
            Guid.NewGuid(), id, null, WarrantyClaimStatus.Open, openedAt, byUserId, null));

        return claim;
    }

    /// <summary>Records that this claim was sent to the manufacturer.</summary>
    public void Submit(DateTimeOffset at, Guid? byUserId, string? note) =>
        Move(WarrantyClaimStatus.Submitted, at, byUserId, note);

    /// <summary>Records that the manufacturer agreed to pay it.</summary>
    public void Approve(DateTimeOffset at, Guid? byUserId, string? note) =>
        Move(WarrantyClaimStatus.Approved, at, byUserId, note);

    /// <summary>
    /// Records that the manufacturer refused it. Needs a reason — the same
    /// discipline a reopened accounting period does, because a claim that just
    /// stops here with no explanation is a dead end nobody can act on later.
    /// </summary>
    public void Deny(DateTimeOffset at, Guid? byUserId, string reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new ArgumentException("Denying a claim needs a reason on the record.", nameof(reason));
        }

        Move(WarrantyClaimStatus.Denied, at, byUserId, reason.Trim());
    }

    /// <summary>
    /// Records what the manufacturer actually paid, which stands even when it
    /// differs from what was billed.
    /// </summary>
    public void RecordPaid(decimal amountPaid, DateTimeOffset at, Guid? byUserId, string? note)
    {
        if (amountPaid <= 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(amountPaid), "A payment is for an amount above zero.");
        }

        AmountPaid = amountPaid;
        Move(WarrantyClaimStatus.Paid, at, byUserId, note);
    }

    private void Move(WarrantyClaimStatus next, DateTimeOffset at, Guid? byUserId, string? note)
    {
        if (!WarrantyClaimRules.CanMove(Status, next))
        {
            var options = WarrantyClaimRules.MovesFrom(Status);
            throw new InvalidOperationException(
                options.Count == 0
                    ? $"A {Status} claim is finished."
                    : $"A {Status} claim cannot become {next}. It can become: {string.Join(", ", options)}.");
        }

        _history.Add(new WarrantyClaimStatusChange(Guid.NewGuid(), Id, Status, next, at, byUserId, note));
        Status = next;
    }
}

/// <summary>
/// The legal moves between claim statuses. A claim that could jump straight
/// from Open to Paid would be a claim nobody actually sent anywhere.
/// </summary>
public static class WarrantyClaimRules
{
    private static readonly Dictionary<WarrantyClaimStatus, WarrantyClaimStatus[]> Allowed = new()
    {
        [WarrantyClaimStatus.Open] = [WarrantyClaimStatus.Submitted, WarrantyClaimStatus.Denied],
        [WarrantyClaimStatus.Submitted] = [WarrantyClaimStatus.Approved, WarrantyClaimStatus.Denied],
        [WarrantyClaimStatus.Approved] = [WarrantyClaimStatus.Paid, WarrantyClaimStatus.Denied],
        [WarrantyClaimStatus.Denied] = [],
        [WarrantyClaimStatus.Paid] = [],
    };

    public static bool CanMove(WarrantyClaimStatus from, WarrantyClaimStatus to) =>
        from != to && Allowed[from].Contains(to);

    /// <summary>The moves available from a status, for a screen to offer.</summary>
    public static IReadOnlyList<WarrantyClaimStatus> MovesFrom(WarrantyClaimStatus from) => Allowed[from];
}

/// <summary>
/// One transition of a claim. Append-only, the same reason
/// AccountingPeriodChange is: a claim that was submitted, denied, resubmitted
/// and eventually paid is exactly the sequence somebody chasing it later needs
/// to reconstruct.
/// </summary>
public sealed class WarrantyClaimStatusChange : IAppendOnly
{
    public Guid Id { get; private set; }

    public Guid WarrantyClaimId { get; private set; }

    public WarrantyClaimStatus? FromStatus { get; private set; }

    public WarrantyClaimStatus ToStatus { get; private set; }

    public DateTimeOffset OccurredAt { get; private set; }

    /// <summary>Insertion order, assigned by the database. See AccountingPeriodChange.Sequence.</summary>
    public long Sequence { get; private set; }

    public Guid? ChangedByUserId { get; private set; }

    public string? Note { get; private set; }

    private WarrantyClaimStatusChange()
    {
    }

    internal WarrantyClaimStatusChange(
        Guid id,
        Guid warrantyClaimId,
        WarrantyClaimStatus? fromStatus,
        WarrantyClaimStatus toStatus,
        DateTimeOffset occurredAt,
        Guid? changedByUserId,
        string? note)
    {
        Id = id;
        WarrantyClaimId = warrantyClaimId;
        FromStatus = fromStatus;
        ToStatus = toStatus;
        OccurredAt = occurredAt;
        ChangedByUserId = changedByUserId;
        Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
    }
}
