// Copyright (c) 2026 The DealerFOSS contributors.
// SPDX-License-Identifier: AGPL-3.0-or-later
//
// Overview: Purpose, File Design, and Engineering
//   InventoryStatusChange — one line of a unit's history: it moved from here to
//   there, then, and this is why.
//
// Usage:
//   Written by InventoryUnit.ChangeStatus; never constructed directly.
//
// Coding Instructions:
//   History is append-only. A wrong entry is corrected by making the opposite
//   move with a reason, not by editing the record (doc 04 §4, ADR-016).
//   TenantDb refuses to update or delete these rows.

using DealerFOSS.Core;

namespace DealerFOSS.Inventory;

public sealed class InventoryStatusChange : IAppendOnly
{
    public Guid Id { get; private set; }

    public Guid InventoryUnitId { get; private set; }

    /// <summary>Null on the first entry — the unit did not come from anywhere.</summary>
    public InventoryStatus? FromStatus { get; private set; }

    public InventoryStatus ToStatus { get; private set; }

    public DateTimeOffset OccurredAt { get; private set; }

    /// <summary>
    /// Insertion order, assigned by the database. This is what makes the history a
    /// sequence rather than a set: two entries can carry the same OccurredAt to the
    /// microsecond, and ordering on the timestamp alone lets the store return tied
    /// rows in any order — which it did, showing a car available before it arrived.
    /// Never set in code; the column is an IDENTITY.
    /// </summary>
    public long Sequence { get; private set; }

    /// <summary>Who made the move, or null when the system did.</summary>
    public Guid? ChangedByUserId { get; private set; }

    public string? Note { get; private set; }

    private InventoryStatusChange()
    {
    }

    internal InventoryStatusChange(
        Guid id,
        Guid inventoryUnitId,
        InventoryStatus? fromStatus,
        InventoryStatus toStatus,
        DateTimeOffset occurredAt,
        Guid? changedByUserId,
        string? note)
    {
        Id = id;
        InventoryUnitId = inventoryUnitId;
        FromStatus = fromStatus;
        ToStatus = toStatus;
        OccurredAt = occurredAt;
        ChangedByUserId = changedByUserId;
        Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
    }
}
