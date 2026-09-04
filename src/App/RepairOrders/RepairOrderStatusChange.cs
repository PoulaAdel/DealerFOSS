// Copyright (c) 2026 The DealerFOSS contributors.
// SPDX-License-Identifier: AGPL-3.0-or-later
//
// Overview: Purpose, File Design, and Engineering
//   RepairOrderStatusChange — one line of a job's history: it moved from here to
//   there, then, by whom, and why.
//
// Usage:
//   Written by RepairOrder.ChangeStatus; never constructed directly.
//
// Coding Instructions:
//   Append-only (ADR-016). This is the record of what a car was billed for
//   and when — a service invoice is the document a customer disputes months
//   later, and a history that can be rewritten answers nothing. TenantDb
//   refuses to update or delete anything marked IAppendOnly.

using DealerFOSS.Core;

namespace DealerFOSS.RepairOrders;

public sealed class RepairOrderStatusChange : IAppendOnly
{
    public Guid Id { get; private set; }

    public Guid RepairOrderId { get; private set; }

    /// <summary>Null on the first entry — the job did not come from anywhere.</summary>
    public RepairOrderStatus? FromStatus { get; private set; }

    public RepairOrderStatus ToStatus { get; private set; }

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

    /// <summary>
    /// The billable total at the moment of the move, so an invoice records the
    /// number that was invoiced rather than whatever the lines say today.
    /// </summary>
    public decimal AmountAtChange { get; private set; }

    private RepairOrderStatusChange()
    {
    }

    internal RepairOrderStatusChange(
        Guid id,
        Guid repairOrderId,
        RepairOrderStatus? fromStatus,
        RepairOrderStatus toStatus,
        DateTimeOffset occurredAt,
        Guid? changedByUserId,
        string? note,
        decimal amountAtChange)
    {
        Id = id;
        RepairOrderId = repairOrderId;
        FromStatus = fromStatus;
        ToStatus = toStatus;
        OccurredAt = occurredAt;
        ChangedByUserId = changedByUserId;
        Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
        AmountAtChange = amountAtChange;
    }
}
