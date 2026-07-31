// InventoryStatusChange — one line of a unit's history: it moved from here to
// there, then, and this is why.
//
// Use:  written by InventoryUnit.ChangeStatus; never constructed directly.
// Edit: history is append-only. A wrong entry is corrected by making the opposite
//       move with a reason, not by editing the record (doc 04 §4, ADR-016).
//       TenantDb refuses to update or delete these rows.

using OpenDealer360.Core;

namespace OpenDealer360.Inventory;

public sealed class InventoryStatusChange : IAppendOnly
{
    public Guid Id { get; private set; }

    public Guid InventoryUnitId { get; private set; }

    /// <summary>Null on the first entry — the unit did not come from anywhere.</summary>
    public InventoryStatus? FromStatus { get; private set; }

    public InventoryStatus ToStatus { get; private set; }

    public DateTimeOffset OccurredAt { get; private set; }

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
