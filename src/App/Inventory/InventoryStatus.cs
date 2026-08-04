// InventoryStatus — where a unit is in its life on the lot, and which moves are
// legal between those states.
//
// Use:  InventoryStatusRules.CanMove(from, to) before changing a status;
//       InventoryUnit.ChangeStatus already applies it.
// Edit: adding a status means deciding what it can move to AND what can move to
//       it. Leaving either out silently strands units in a state nobody can get
//       them out of.

namespace DealerFOSS.Inventory;

public enum InventoryStatus
{
    /// <summary>Bought, not yet physically here.</summary>
    Incoming = 0,

    /// <summary>Here, but not sellable until work is finished.</summary>
    Reconditioning = 1,

    /// <summary>On the lot and sellable.</summary>
    Available = 2,

    /// <summary>Spoken for but not sold — a deposit, a pending deal, a loaner.</summary>
    OnHold = 3,

    /// <summary>Sold to a customer.</summary>
    Sold = 4,

    /// <summary>Gone without a sale — wholesaled, returned, or entered in error.</summary>
    Removed = 5,
}

/// <summary>
/// The legal moves between inventory statuses. A dealership's stock moves in a
/// small number of understood ways; anything else is a mistake worth refusing at
/// the point it is made rather than reconciling later.
/// </summary>
public static class InventoryStatusRules
{
    private static readonly Dictionary<InventoryStatus, InventoryStatus[]> Allowed = new()
    {
        [InventoryStatus.Incoming] =
            [InventoryStatus.Reconditioning, InventoryStatus.Available, InventoryStatus.Removed],
        [InventoryStatus.Reconditioning] =
            [InventoryStatus.Available, InventoryStatus.OnHold, InventoryStatus.Removed],
        [InventoryStatus.Available] =
            [InventoryStatus.Reconditioning, InventoryStatus.OnHold, InventoryStatus.Sold, InventoryStatus.Removed],
        [InventoryStatus.OnHold] =
            [InventoryStatus.Available, InventoryStatus.Reconditioning, InventoryStatus.Sold, InventoryStatus.Removed],

        // A deal does fall through, and the car goes back on the lot. That is a
        // status change with a reason, not a deleted record.
        [InventoryStatus.Sold] =
            [InventoryStatus.Available, InventoryStatus.Removed],

        // Removed is the end. A vehicle that comes back is received again as a
        // new unit, so its second stay has its own history.
        [InventoryStatus.Removed] = [],
    };

    /// <summary>Whether a unit may move from one status to another.</summary>
    public static bool CanMove(InventoryStatus from, InventoryStatus to) =>
        from != to && Allowed[from].Contains(to);

    /// <summary>The moves available from a status, for a screen to offer.</summary>
    public static IReadOnlyList<InventoryStatus> MovesFrom(InventoryStatus from) => Allowed[from];
}
