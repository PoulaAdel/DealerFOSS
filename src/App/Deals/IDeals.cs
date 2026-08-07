// IDeals — what other capabilities may call to reach deals.
//
// Use:  Finance and Accounting will read a delivered deal through this.
// Edit: every read behind this interface is filtered to the caller's authorized
//       rooftops. An empty scope is a denial, never "unfiltered".

using DealerFOSS.Core;

namespace DealerFOSS.Deals;

/// <summary>
/// Deals being worked at a rooftop: one customer, one car, a price, and an
/// approval. Rooftop-owned and scoped (doc 04 §1).
/// </summary>
public interface IDeals
{
    Task<Result<IReadOnlyList<DealSummary>>> ListAsync(DealQuery query, CancellationToken cancellationToken);

    Task<Result<DealDetail>> GetAsync(Guid dealId, CancellationToken cancellationToken);

    Task<Result<DealDetail>> StartAsync(NewDeal deal, CancellationToken cancellationToken);

    Task<Result<DealDetail>> SetTermsAsync(Guid dealId, DealTerms terms, CancellationToken cancellationToken);

    /// <summary>
    /// Replaces the F&amp;I products sold on the deal. Separate from the terms
    /// because the two are set by different people at different moments — the
    /// salesperson prices the car, the F&amp;I manager sells the products
    /// afterwards — and one call replacing both would let either wipe the other's
    /// work.
    /// </summary>
    Task<Result<DealDetail>> SetProductsAsync(
        Guid dealId,
        IReadOnlyList<SoldProduct> products,
        CancellationToken cancellationToken);

    Task<Result<DealDetail>> ChangeStatusAsync(
        Guid dealId,
        DealStatusChangeRequest change,
        CancellationToken cancellationToken);
}

/// <summary>One deal as a desk list shows it.</summary>
public sealed record DealSummary(
    Guid Id,
    RooftopId RooftopId,
    string Status,
    Guid CustomerId,
    string CustomerName,
    Guid InventoryUnitId,
    string StockNumber,
    string Vehicle,
    decimal AmountDue,
    string Currency,
    Guid? SalespersonUserId,
    bool IsApproved);

/// <summary>One deal in full, with its numbers and everything that happened to it.</summary>
public sealed record DealDetail(
    Guid Id,
    RooftopId RooftopId,
    string Status,
    Guid CustomerId,
    string CustomerName,
    Guid InventoryUnitId,
    string StockNumber,
    string Vehicle,
    Guid? LeadId,
    string Currency,
    decimal Subtotal,
    decimal AmountDue,
    TradeInView? TradeIn,
    IReadOnlyList<ChargeView> Charges,

    /// <summary>What was sold alongside the car, with what each one made.</summary>
    IReadOnlyList<DealProductView> Products,

    /// <summary>
    /// What the products made in total. Reported separately from the car because
    /// a dealer principal reads them as two businesses — and on many deals this is
    /// the larger one.
    /// </summary>
    decimal ProductGross,
    Guid? SalespersonUserId,
    Guid? ApprovedByUserId,
    DateTimeOffset? ApprovedAt,
    bool TermsAreOpen,
    IReadOnlyList<DealHistoryEntry> History);

public sealed record ChargeView(string Kind, string Description, decimal Amount);

/// <summary>
/// One product sold on this deal. <c>Cost</c> and <c>Gross</c> are the
/// dealership's own figures and never appear on anything the customer is handed.
/// </summary>
public sealed record DealProductView(
    Guid Id,
    Guid FinanceProductId,
    string Name,
    string? Provider,
    decimal Price,
    decimal Cost,
    decimal Gross,
    int? TermMonths,
    int? TermMiles);

/// <summary>
/// A product being sold, at the price and cost agreed for this deal. Both are
/// stated rather than read from the catalogue — F&amp;I is negotiated, and next
/// month's price list must not rewrite this month's gross.
/// </summary>
public sealed record SoldProduct(
    Guid FinanceProductId,
    decimal Price,
    decimal Cost,
    int? TermMonths = null,
    int? TermMiles = null);

public sealed record TradeInView(
    string Description,
    decimal Allowance,
    decimal Payoff,
    decimal Equity,
    bool IsNegativeEquity);

public sealed record DealHistoryEntry(
    string? FromStatus,
    string ToStatus,
    DateTimeOffset OccurredAt,
    Guid? ChangedByUserId,
    string? Note,
    decimal AmountAtChange);

/// <summary>How a caller narrows a deal list.</summary>
public sealed record DealQuery(
    RooftopId? RooftopId = null,
    string? Status = null,
    Guid? CustomerId = null,
    Guid? SalespersonUserId = null,
    bool OpenOnly = false,
    int Limit = 50);

/// <summary>What a caller supplies to start a deal.</summary>
public sealed record NewDeal(
    RooftopId RooftopId,
    Guid CustomerId,
    Guid InventoryUnitId,
    string Currency,
    Guid? SalespersonUserId = null,
    Guid? LeadId = null);

/// <summary>The numbers on a deal. Replaces whatever was there before.</summary>
public sealed record DealTerms(
    IReadOnlyList<NewCharge> Charges,
    NewTradeIn? TradeIn = null);

public sealed record NewCharge(string Kind, string Description, decimal Amount);

public sealed record NewTradeIn(string Description, decimal Allowance, decimal Payoff);

/// <summary>What a caller supplies to move a deal on.</summary>
public sealed record DealStatusChangeRequest(string Status, string? Note = null);
