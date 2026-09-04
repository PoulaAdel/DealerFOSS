// Copyright (c) 2026 The DealerFOSS contributors.
// SPDX-License-Identifier: AGPL-3.0-or-later
//
// Overview: Purpose, File Design, and Engineering
//   DealStatus — where a deal has got to, and which moves are legal between those
//   states.
//
// Usage:
//   DealStatusRules.CanMove(from, to) before changing a status;
//   Deal.ChangeStatus already applies it.
//
// Coding Instructions:
//   The Draft boundary is the important one. While a deal is Draft its
//   numbers can be changed freely; from Submitted onwards they are frozen and
//   a change means going back to Draft, which is a recorded move. That is
//   what stops a price quietly changing after a manager approved it.

namespace DealerFOSS.Deals;

public enum DealStatus
{
    /// <summary>Being worked. The numbers can still change.</summary>
    Draft = 0,

    /// <summary>Sent to a manager. The numbers are frozen from here.</summary>
    Submitted = 1,

    /// <summary>A manager signed it off.</summary>
    Approved = 2,

    /// <summary>The customer took the car. The end of the line.</summary>
    Delivered = 3,

    /// <summary>It did not happen. The car goes back on the lot.</summary>
    Cancelled = 4,
}

/// <summary>
/// The legal moves between deal statuses. A deal that could jump straight from
/// Draft to Delivered would be a deal nobody approved.
/// </summary>
public static class DealStatusRules
{
    private static readonly Dictionary<DealStatus, DealStatus[]> Allowed = new()
    {
        [DealStatus.Draft] = [DealStatus.Submitted, DealStatus.Cancelled],

        // Sent back for changes is an ordinary outcome, not a failure.
        [DealStatus.Submitted] = [DealStatus.Approved, DealStatus.Draft, DealStatus.Cancelled],

        // An approved deal can still fall through — financing declines, the
        // customer changes their mind — right up until the car leaves.
        [DealStatus.Approved] = [DealStatus.Delivered, DealStatus.Cancelled],

        [DealStatus.Delivered] = [],
        [DealStatus.Cancelled] = [],
    };

    public static bool CanMove(DealStatus from, DealStatus to) =>
        from != to && Allowed[from].Contains(to);

    /// <summary>The moves available from a status, for a screen to offer.</summary>
    public static IReadOnlyList<DealStatus> MovesFrom(DealStatus from) => Allowed[from];

    /// <summary>Whether the numbers may still be edited.</summary>
    public static bool TermsAreOpen(DealStatus status) => status == DealStatus.Draft;

    /// <summary>Whether the deal is finished, one way or the other.</summary>
    public static bool IsClosed(DealStatus status) =>
        status is DealStatus.Delivered or DealStatus.Cancelled;
}

/// <summary>What a line on the deal is for.</summary>
public enum ChargeKind
{
    /// <summary>The price of the car itself. Exactly one per deal.</summary>
    VehiclePrice = 0,

    /// <summary>Documentation, registration, delivery — anything added on.</summary>
    Fee = 1,

    /// <summary>Money off. Always negative.</summary>
    Discount = 2,

    /// <summary>Mats, paint protection, a tow bar.</summary>
    Accessory = 3,
}
