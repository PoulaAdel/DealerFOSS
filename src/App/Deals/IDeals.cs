// Copyright (c) 2026 The DealerFOSS contributors.
// SPDX-License-Identifier: AGPL-3.0-or-later
//
// Overview: Purpose, File Design, and Engineering
//   IDeals — what other capabilities may call to reach deals.
//
// Usage:
//   Finance and Accounting will read a delivered deal through this.
//
// Coding Instructions:
//   Every read behind this interface is filtered to the caller's authorized
//   rooftops. An empty scope is a denial, never "unfiltered".

using DealerFOSS.Core;

namespace DealerFOSS.Deals;

/// <summary>
/// Deals being worked at a rooftop: one customer, one car, a price, and an
/// approval. Rooftop-owned and scoped (doc 04 §1).
/// </summary>
public interface IDeals
{
    Task<Result<Page<DealSummary>>> ListAsync(DealQuery query, CancellationToken cancellationToken);

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

    /// <summary>
    /// Replaces the tax on a Draft deal. Every line says where its figure came
    /// from, and "a person typed it" is a valid answer — that is what lets the
    /// product work in a jurisdiction nobody has written a pack for (ADR-024 R5).
    /// </summary>
    Task<Result<DealDetail>> SetTaxAsync(
        Guid dealId,
        DealTaxEntry tax,
        CancellationToken cancellationToken);

    /// <summary>
    /// Replaces the address the car will be registered or garaged at, or clears
    /// it with null. Distinct from the customer's own mailing address — this is
    /// the fact ADR-024 traces tax to, frozen with the deal once it leaves Draft.
    /// </summary>
    Task<Result<DealDetail>> SetRegistrationAddressAsync(
        Guid dealId,
        RegistrationAddressView? address,
        CancellationToken cancellationToken);

    Task<Result<DealDetail>> ChangeStatusAsync(
        Guid dealId,
        DealStatusChangeRequest change,
        CancellationToken cancellationToken);

    /// <summary>
    /// Cancels one F&amp;I product already sold on a delivered deal. The original
    /// sale is left exactly as agreed; this records a separate event and, when
    /// something is owed back, raises a credit the customer can have applied to
    /// what they still owe or handed back — the same mechanism an overpayment
    /// uses.
    /// </summary>
    Task<Result<DealDetail>> CancelProductAsync(
        Guid dealId,
        Guid dealProductId,
        CancelProduct cancellation,
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

    /// <summary>Every tax charged, each saying where its figure came from.</summary>
    IReadOnlyList<TaxLineView> TaxLines,

    /// <summary>The tax added up, so a screen does not have to.</summary>
    decimal TaxTotal,

    /// <summary>The address the tax was worked out from. Null when there is no tax.</summary>
    TaxAddressView? TaxedAt,

    /// <summary>
    /// Where the car will be registered or garaged. Distinct from the customer's
    /// own mailing address, and from <see cref="TaxedAt"/> — this is the fact
    /// tax is traced to (ADR-024); TaxedAt is the narrower snapshot copied onto
    /// the tax lines once it has been worked out. Null until somebody sets it.
    /// </summary>
    RegistrationAddressView? RegistrationAddress,

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
    int? TermMiles,
    bool IsCancelled,
    DateTimeOffset? CancelledAt,
    decimal? RefundAmount,
    string? CancellationReason);

/// <summary>What a caller supplies to cancel a sold product.</summary>
/// <param name="RefundAmount">
/// What is owed back to the customer, capped at the product's own price. Zero
/// is a real answer — a product cancelled inside a non-refundable window.
/// </param>
public sealed record CancelProduct(decimal RefundAmount, string? Reason = null);

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
    int Limit = 50,
    int Offset = 0);

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

/// <summary>
/// The tax to record on a deal, and the address it was worked out from.
/// </summary>
/// <remarks>
/// The address is required as soon as there is a line, because it is how a rate
/// is defended later. Sending no lines clears the tax and the address together.
/// </remarks>
public sealed record DealTaxEntry(
    IReadOnlyList<NewTaxLine> Lines,
    TaxAddressView? TaxedAt);

/// <summary>
/// One tax to charge. <c>Provenance</c> is required and says where the figure
/// came from — a person, a jurisdiction pack, or a provider.
/// </summary>
/// <param name="Rate">
/// A fraction, not a percentage: 0.0625 is six and a quarter percent. Zero when
/// a person typed the amount rather than working it out from a rate.
/// </param>
public sealed record NewTaxLine(
    string Description,
    string Jurisdiction,
    decimal Basis,
    decimal Rate,
    decimal Amount,
    string Provenance,
    string? PackId = null,
    int? PackVersion = null);

public sealed record TaxLineView(
    Guid Id,
    string Description,
    string Jurisdiction,
    decimal Basis,
    decimal Rate,
    decimal Amount,
    string Provenance,
    string? PackId,
    int? PackVersion);

/// <summary>State and county separately, because a US rate depends on both.</summary>
public sealed record TaxAddressView(
    string? AdministrativeArea,
    string? County,
    string? PostalCode,
    string Country);

/// <summary>
/// The full postal shape, because this address ends up on registration
/// paperwork and not only on a rate lookup — unlike <see cref="TaxAddressView"/>.
/// </summary>
public sealed record RegistrationAddressView(
    string Line1,
    string? Line2,
    string City,
    string? AdministrativeArea,
    string? County,
    string? PostalCode,
    string Country);
