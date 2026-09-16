// Copyright (c) 2026 The DealerFOSS contributors.
// SPDX-License-Identifier: AGPL-3.0-or-later
//
// Overview: Purpose, File Design, and Engineering
//   ServiceLine — one piece of work on a repair order: labour, a part, or a job
//   sent out.
//
// Usage:
//   Added through RepairOrder.AddLine, never constructed directly, so the
//   arithmetic and the authorization state always start out consistent.
//
// Coding Instructions:
//   Labour is stored as hours AND a rate, not just a total. A service
//   department's entire margin conversation is "how long did that take
//   against what we charged for it", and a line that only kept the money
//   cannot answer it. The total is derived, so the two can never disagree.
//
//   A line the customer declined keeps its amount rather than being deleted.
//   "We offered, they said no" is worth more than silence when the same car
//   comes back with the same fault.

namespace DealerFOSS.RepairOrders;

public sealed class ServiceLine
{
    public Guid Id { get; private set; }

    public Guid RepairOrderId { get; private set; }

    public ServiceLineKind Kind { get; private set; }

    public string Description { get; private set; } = string.Empty;

    /// <summary>Hours booked. Null on anything that is not labour.</summary>
    public decimal? Hours { get; private set; }

    /// <summary>Charged per hour. Null on anything that is not labour.</summary>
    public decimal? Rate { get; private set; }

    /// <summary>What a part or a sublet job costs the customer. Zero for labour.</summary>
    public decimal UnitAmount { get; private set; }

    /// <summary>
    /// Who pays for this line. Set per line, not per job, because one job
    /// routinely mixes them: the customer came in for a service, the water pump
    /// turned out to be under warranty, and the workshop replaced a wiper blade
    /// off its own stock while the car was up.
    /// </summary>
    public ServicePayType PayType { get; private set; }

    public LineAuthorization Authorization { get; private set; }

    public DateTimeOffset? AuthorizedAt { get; private set; }

    /// <summary>Who recorded the customer's answer. Not the customer themselves.</summary>
    public Guid? AuthorizedByUserId { get; private set; }

    /// <summary>How the answer was obtained — "phoned, agreed 10:40".</summary>
    public string? AuthorizationNote { get; private set; }

    /// <summary>
    /// The catalogued job this line sells, when it is one. Null is a legitimate
    /// choice for the same reason a part line may carry no PartId: a one-off job
    /// nobody will ever do again still has to be billable.
    /// </summary>
    /// <remarks>
    /// Kept as a REFERENCE rather than by copying the code onto the line,
    /// because the description, hours and rate are already copied — those are
    /// what the customer was quoted, and they must not move when somebody
    /// revises the catalogue. The id is provenance only: which entry this came
    /// from.
    /// </remarks>
    public Guid? OpCodeId { get; private set; }

    /// <summary>
    /// The catalogue part this line sells, when it is one. Null means a part
    /// typed in by hand — still billable, still on the record, but nothing comes
    /// off a shelf for it and it contributes no cost. Both are legitimate: a
    /// one-off item bought for a single job never enters the catalogue.
    /// </summary>
    public Guid? PartId { get; private set; }

    /// <summary>How many come off the shelf. Null when this line sells no stock.</summary>
    public decimal? PartQuantity { get; private set; }

    /// <summary>
    /// What the parts on this line cost, worked out and frozen at the moment the
    /// job was invoiced. Never recalculated — a supplier price rise must not
    /// rewrite what last month's work cost.
    /// </summary>
    public decimal? CostAmount { get; private set; }

    /// <summary>Whether this line sells stock that has to come off a shelf.</summary>
    public bool DrawsFromStock => PartId is not null && PartQuantity is > 0m;

    /// <summary>
    /// What this line adds to the bill. Labour multiplies out; everything else is
    /// its own amount. A declined line is worth nothing, which is what keeps it
    /// visible on the record without reaching the total.
    /// </summary>
    public decimal Amount => Authorization == LineAuthorization.Declined
        ? 0m
        : Kind == ServiceLineKind.Labour
            ? Math.Round((Hours ?? 0m) * (Rate ?? 0m), 2, MidpointRounding.AwayFromZero)
            : UnitAmount;

    private ServiceLine()
    {
    }

    internal ServiceLine(
        Guid id,
        Guid repairOrderId,
        ServiceLineKind kind,
        string description,
        decimal? hours,
        decimal? rate,
        decimal unitAmount,
        LineAuthorization authorization,
        DateTimeOffset? authorizedAt,
        Guid? authorizedByUserId,
        Guid? partId = null,
        decimal? partQuantity = null,
        ServicePayType payType = ServicePayType.CustomerPay,
        Guid? opCodeId = null)
    {
        if (string.IsNullOrWhiteSpace(description))
        {
            throw new ArgumentException("A line needs a description.", nameof(description));
        }

        if (opCodeId is not null && kind != ServiceLineKind.Labour)
        {
            // An op code IS a job -- a standard time at a standard price. Citing one
            // on a part or a sublet line would mean nothing, and would let the
            // op-code reports count work that was never done.
            throw new ArgumentException(
                "Only a Labour line can cite an op code.", nameof(opCodeId));
        }

        if (partId is not null && kind != ServiceLineKind.Part)
        {
            throw new ArgumentException(
                "Only a Part line can draw from stock — labour and sublet work have no shelf.",
                nameof(partId));
        }

        if (partId is not null && partQuantity is null or <= 0m)
        {
            throw new ArgumentException(
                "A line that draws from stock needs a quantity.", nameof(partQuantity));
        }

        if (kind == ServiceLineKind.Labour)
        {
            if (hours is null or <= 0m)
            {
                throw new ArgumentException("Labour needs hours.", nameof(hours));
            }

            if (rate is null or < 0m)
            {
                throw new ArgumentException("Labour needs a rate.", nameof(rate));
            }
        }
        else
        {
            if (unitAmount < 0m)
            {
                throw new ArgumentException($"A {kind} cannot cost less than nothing.", nameof(unitAmount));
            }
        }

        Id = id;
        RepairOrderId = repairOrderId;
        Kind = kind;
        Description = description.Trim();
        Hours = kind == ServiceLineKind.Labour ? hours : null;
        Rate = kind == ServiceLineKind.Labour ? rate : null;
        UnitAmount = kind == ServiceLineKind.Labour ? 0m : unitAmount;
        PayType = payType;
        Authorization = authorization;
        AuthorizedAt = authorizedAt;
        AuthorizedByUserId = authorizedByUserId;
        PartId = partId;
        PartQuantity = partId is null ? null : partQuantity;
        OpCodeId = opCodeId;
    }

    /// <summary>
    /// Freezes what the parts on this line cost. Called once, while invoicing,
    /// with the figure the costing method produced at that moment. Deliberately
    /// has no way to be called again — see CostAmount.
    /// </summary>
    internal void RecordCost(decimal cost)
    {
        if (CostAmount is not null)
        {
            throw new InvalidOperationException("This line already has a cost recorded against it.");
        }

        CostAmount = cost;
    }

    /// <summary>
    /// Records what the customer said. One answer only: re-asking a line that has
    /// already been answered would overwrite the timestamp on the conversation
    /// that actually happened.
    /// </summary>
    internal void Answer(
        bool approved,
        DateTimeOffset answeredAt,
        Guid? answeredByUserId,
        string? note)
    {
        if (Authorization != LineAuthorization.Pending)
        {
            throw new InvalidOperationException(
                $"This line was already {Authorization.ToString().ToLowerInvariant()}. "
                + "Add a new line if the customer has changed their mind.");
        }

        Authorization = approved ? LineAuthorization.Authorized : LineAuthorization.Declined;
        AuthorizedAt = answeredAt;
        AuthorizedByUserId = answeredByUserId;
        AuthorizationNote = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
    }
}
