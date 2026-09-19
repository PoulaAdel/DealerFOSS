// Copyright (c) 2026 The DealerFOSS contributors.
// SPDX-License-Identifier: AGPL-3.0-or-later
//
// Overview: Purpose, File Design, and Engineering
//   ReconditioningCharge — what one stay in stock absorbed on the way to being
//   saleable, one row per posting.
//
// Usage:
//   InventoryService.CapitaliseReconditioningAsync writes these when the
//   workshop invoices internal work on a car the dealership owns. Nothing else
//   should; a charge with no posting behind it is a carrying value nobody can
//   explain.
//
// Coding Instructions:
//   THIS EXISTS BECAUSE THE MONEY WENT IN AND NEVER CAME OUT. Found 2026-09-19.
//   Invoicing internal work debited 1300 Vehicle inventory by the recon spend —
//   correctly, and the posting's own comment says why — but nothing recorded
//   WHICH car absorbed it, so delivery relieved 1300 by the unit's acquisition
//   cost alone. Two things followed, and the second is the worse one:
//
//     - used-vehicle gross was overstated by exactly the recon spend, which is
//       the precise failure that posting's comment says it exists to prevent;
//     - 1300 grew forever. Every reconditioned car left its recon behind, so
//       vehicle inventory on the balance sheet drifted permanently upward and
//       stopped tying to the cars actually on the lot.
//
//   Neither was catchable by the balancing checks, because each entry balanced
//   on its own. It was an account that never returned to zero, not an entry
//   that did not add up.
//
//   ONE ROW PER POSTING, NOT A RUNNING TOTAL ON THE UNIT. "Where did this
//   $1,200 of carrying value come from" has to have an answer, and a column
//   that has been incremented four times cannot give one. This is the same
//   reasoning that made CustomerCredit a row drawn down by uses rather than a
//   balance.
//
//   APPEND-ONLY, and that is load bearing rather than decorative: a correction
//   is a NEGATIVE row naming the same repair order, never an edit and never a
//   delete. A carrying value that can be quietly rewritten is a carrying value
//   nobody can audit. (Contrast TechnicianClocking, which was marked
//   IAppendOnly in error because closing a clocking is an update. Nothing ever
//   updates one of these.)
//
//   ONE STAY IN STOCK IS ONE UNIT, so the unit id is enough and no separate
//   "stock stay" is modelled. InventoryService.FindOwnedAsync excludes Sold and
//   Removed, so a car that leaves and comes back is received as a new unit with
//   a new stock number and starts from nothing. If that ever stops being true,
//   this is the file that breaks first.

using DealerFOSS.Core;

namespace DealerFOSS.Inventory;

public sealed class ReconditioningCharge : IAppendOnly
{
    public Guid Id { get; private set; }

    /// <summary>The car that absorbed it. One stay in stock — see the header.</summary>
    public Guid InventoryUnitId { get; private set; }

    /// <summary>Copied from the unit, so this is filterable without a join.</summary>
    public RooftopId RooftopId { get; private set; }

    /// <summary>
    /// Positive when work was capitalised onto the car, negative when a posting
    /// was corrected. Never edited — see the header.
    /// </summary>
    public decimal Amount { get; private set; }

    /// <summary>
    /// ISO 4217. Always the unit's own currency: capitalising a charge in one
    /// currency onto a car costed in another would produce a carrying value
    /// that is not a number, so the service refuses it rather than converting.
    /// </summary>
    public string Currency { get; private set; } = string.Empty;

    /// <summary>
    /// The repair order whose invoice posted this. The other half of the audit
    /// trail: the journal entry for that invoice carries the same id in its
    /// Reference, so 1300 can be walked back to a car and a car forward to its
    /// postings without the ledger needing a subject column of its own.
    /// </summary>
    public Guid SourceRepairOrderId { get; private set; }

    public DateTimeOffset OccurredAt { get; private set; }

    private ReconditioningCharge()
    {
    }

    /// <summary>
    /// Records work capitalised onto a car. <paramref name="amount"/> may be
    /// negative to correct an earlier posting, but never zero — a row that
    /// changes nothing is noise in the one place that has to stay readable.
    /// </summary>
    public static Result<ReconditioningCharge> Record(
        Guid inventoryUnitId,
        RooftopId rooftopId,
        Money amount,
        Guid sourceRepairOrderId,
        DateTimeOffset occurredAt)
    {
        if (amount.Amount == 0m)
        {
            return Result.Failure<ReconditioningCharge>(InventoryErrors.ReconditioningIsNothing);
        }

        return Result.Success(new ReconditioningCharge
        {
            Id = Guid.NewGuid(),
            InventoryUnitId = inventoryUnitId,
            RooftopId = rooftopId,
            Amount = amount.Amount,
            Currency = amount.Currency,
            SourceRepairOrderId = sourceRepairOrderId,
            OccurredAt = occurredAt,
        });
    }
}
