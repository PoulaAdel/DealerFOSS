// Deal — one customer buying one car, at a price, with a trade-in and an
// approval.
//
// Use:  Deal.Start(...), SetTerms while it is Draft, then ChangeStatus to move it
//       along.
// Edit: two rules here are not conveniences.
//
//       The numbers are frozen the moment the deal leaves Draft. A price that can
//       change after a manager approved it makes the approval meaningless, so
//       changing an approved deal means moving it back to Draft — which is a
//       recorded move somebody has to make deliberately.
//
//       A deal belongs to ONE rooftop and that is a permission boundary
//       (doc 04 §1). The customer is shared across the organization; the deal is
//       not.

using DealerFOSS.Core;

namespace DealerFOSS.Deals;

public sealed class Deal : AuditableEntity
{
    private readonly List<DealCharge> _charges = [];
    private readonly List<DealProduct> _products = [];
    private readonly List<DealStatusChange> _history = [];

    public Guid Id { get; private set; }

    /// <summary>The rooftop selling the car. A permission boundary.</summary>
    public RooftopId RooftopId { get; private set; }

    public Guid CustomerId { get; private set; }

    /// <summary>The specific unit being sold. Held on the lot while this deal lives.</summary>
    public Guid InventoryUnitId { get; private set; }

    /// <summary>The enquiry this came from, when it came from one.</summary>
    public Guid? LeadId { get; private set; }

    /// <summary>ISO 4217. Every amount on the deal is in this currency.</summary>
    public string Currency { get; private set; } = string.Empty;

    public DealStatus Status { get; private set; }

    public Guid? SalespersonUserId { get; private set; }

    /// <summary>Who signed it off. Null until approved, and never the author.</summary>
    public Guid? ApprovedByUserId { get; private set; }

    public DateTimeOffset? ApprovedAt { get; private set; }

    public TradeIn? Trade { get; private set; }

    public IReadOnlyList<DealCharge> Charges => _charges;

    public IReadOnlyList<DealProduct> Products => _products;

    public IReadOnlyList<DealStatusChange> History => _history;

    /// <summary>Everything on the deal added up, before the trade.</summary>
    public Money Subtotal => new(_charges.Sum(c => c.Amount) + _products.Sum(p => p.Price), Currency);

    /// <summary>What the F&amp;I products on this deal sold for.</summary>
    public Money ProductRevenue => new(_products.Sum(p => p.Price), Currency);

    /// <summary>What they cost the dealership.</summary>
    public Money ProductCost => new(_products.Sum(p => p.Cost), Currency);

    /// <summary>
    /// What the dealership made on the products. Reported separately from the car
    /// because a dealer principal reads them as two different businesses, and on
    /// many deals this is the larger of the two.
    /// </summary>
    public Money ProductGross => new(_products.Sum(p => p.Gross), Currency);

    /// <summary>
    /// What the customer actually has to find: the subtotal, less what the trade
    /// is worth, plus whatever is still owed on it.
    /// </summary>
    public Money AmountDue => new(
        _charges.Sum(c => c.Amount) + _products.Sum(p => p.Price)
            - (Trade?.Allowance ?? 0m) + (Trade?.Payoff ?? 0m),
        Currency);

    public bool TermsAreOpen => DealStatusRules.TermsAreOpen(Status);

    private Deal()
    {
    }

    /// <summary>
    /// Starts a deal on a specific car. It begins as Draft with no numbers on it —
    /// the terms are set separately, because the first thing a salesperson does is
    /// pull the car up and the second is start pricing it.
    /// </summary>
    public static Deal Start(
        Guid id,
        RooftopId rooftopId,
        Guid customerId,
        Guid inventoryUnitId,
        string currency,
        DateTimeOffset startedAt,
        Guid? salespersonUserId = null,
        Guid? leadId = null)
    {
        if (customerId == Guid.Empty)
        {
            throw new ArgumentException("A deal needs a customer.", nameof(customerId));
        }

        if (inventoryUnitId == Guid.Empty)
        {
            throw new ArgumentException("A deal needs a car.", nameof(inventoryUnitId));
        }

        // Constructing a Money proves the currency is a real ISO code before it
        // reaches the database.
        var currencyCheck = Money.Zero(currency);

        var deal = new Deal
        {
            Id = id,
            RooftopId = rooftopId,
            CustomerId = customerId,
            InventoryUnitId = inventoryUnitId,
            LeadId = leadId,
            Currency = currencyCheck.Currency,
            Status = DealStatus.Draft,
            SalespersonUserId = salespersonUserId,
        };

        deal._history.Add(new DealStatusChange(
            Guid.NewGuid(), id, null, DealStatus.Draft, startedAt, salespersonUserId, null, 0m));

        return deal;
    }

    /// <summary>
    /// Replaces the numbers on the deal. Only while it is Draft: after that the
    /// figures are what a manager saw, and changing them means going back.
    /// </summary>
    public void SetTerms(IEnumerable<(ChargeKind Kind, string Description, decimal Amount)> charges, TradeIn? trade)
    {
        ArgumentNullException.ThrowIfNull(charges);

        if (!TermsAreOpen)
        {
            throw new InvalidOperationException(
                $"A {Status} deal is frozen. Move it back to Draft to change the numbers.");
        }

        var replacement = charges
            .Select(c => new DealCharge(Guid.NewGuid(), Id, c.Kind, c.Description, c.Amount))
            .ToList();

        if (replacement.Count(c => c.Kind == ChargeKind.VehiclePrice) > 1)
        {
            throw new ArgumentException(
                "A deal has one vehicle price. Extras belong on their own lines.", nameof(charges));
        }

        _charges.Clear();
        _charges.AddRange(replacement);
        Trade = trade;
    }

    /// <summary>
    /// Replaces the F&amp;I products on the deal, each with the price and cost
    /// agreed for THIS deal. Separate from SetTerms because the two are set by
    /// different people at different moments — the salesperson prices the car,
    /// the F&amp;I manager sells the products afterwards — and making one call
    /// replace both would mean either could wipe the other's work.
    /// </summary>
    public void SetProducts(
        IEnumerable<(Guid ProductId, string Name, decimal Price, decimal Cost, int? TermMonths, int? TermMiles)> products)
    {
        ArgumentNullException.ThrowIfNull(products);

        if (!TermsAreOpen)
        {
            throw new InvalidOperationException(
                $"A {Status} deal is frozen. Move it back to Draft to change what was sold.");
        }

        var replacement = products
            .Select(p => new DealProduct(
                Guid.NewGuid(), Id, p.ProductId, p.Name, p.Price, p.Cost, p.TermMonths, p.TermMiles))
            .ToList();

        // The same product twice on one deal is a mistake, not two sales — two
        // warranties on one car is not a thing.
        if (replacement.Select(p => p.FinanceProductId).Distinct().Count() != replacement.Count)
        {
            throw new ArgumentException(
                "The same product appears twice. One deal sells each product once.", nameof(products));
        }

        _products.Clear();
        _products.AddRange(replacement);
    }

    /// <summary>
    /// Moves the deal on and records the move together with the total at that
    /// moment, so an approval records the number that was approved.
    /// </summary>
    public void ChangeStatus(
        DealStatus next,
        DateTimeOffset occurredAt,
        Guid? changedByUserId = null,
        string? note = null)
    {
        if (!DealStatusRules.CanMove(Status, next))
        {
            var options = DealStatusRules.MovesFrom(Status);
            throw new InvalidOperationException(
                options.Count == 0
                    ? $"A {Status} deal is finished."
                    : $"A {Status} deal cannot become {next}. It can become: {string.Join(", ", options)}.");
        }

        if (next == DealStatus.Submitted)
        {
            EnsureReadyToSubmit();
        }

        if (next == DealStatus.Approved)
        {
            EnsureApproverIsNotTheSalesperson(changedByUserId);
        }

        _history.Add(new DealStatusChange(
            Guid.NewGuid(), Id, Status, next, occurredAt, changedByUserId, note, AmountDue.Amount));

        Status = next;

        if (next == DealStatus.Approved)
        {
            ApprovedByUserId = changedByUserId;
            ApprovedAt = occurredAt;
        }

        // Sending it back for changes withdraws the approval with it. Leaving the
        // old approver on a reopened deal would be a lie about who agreed to the
        // new numbers.
        if (next == DealStatus.Draft)
        {
            ApprovedByUserId = null;
            ApprovedAt = null;
        }
    }

    /// <summary>
    /// A salesperson does not approve their own deal — a sales manager does.
    /// Holding the permission is not enough, because a manager who also sells
    /// would otherwise sign off their own numbers.
    /// </summary>
    /// <remarks>
    /// Checked on the entity rather than only in the service, so a background job
    /// or an import cannot route around it. Reassigning the deal to somebody else
    /// and then approving it is still possible, and is deliberately left visible
    /// in the history rather than blocked — the alternative locks a rooftop out
    /// when its salesperson leaves.
    /// </remarks>
    private void EnsureApproverIsNotTheSalesperson(Guid? approver)
    {
        if (approver is { } who && SalespersonUserId == who)
        {
            throw new InvalidOperationException(
                "A deal cannot be approved by the salesperson who built it. "
                + "A sales manager approves it.");
        }
    }

    /// <summary>
    /// A deal reaches a manager only once it says what is being sold and for how
    /// much. Catching this here means the manager's queue holds real deals.
    /// </summary>
    private void EnsureReadyToSubmit()
    {
        if (_charges.Count == 0)
        {
            throw new InvalidOperationException("A deal needs a price before it can be submitted.");
        }

        if (!_charges.Exists(c => c.Kind == ChargeKind.VehiclePrice))
        {
            throw new InvalidOperationException(
                "A deal needs a vehicle price before it can be submitted.");
        }

        if (AmountDue.Amount < 0)
        {
            throw new InvalidOperationException(
                "This deal pays the customer more than they pay the dealership. "
                + "Check the discount and the trade-in allowance.");
        }
    }
}
