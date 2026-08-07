// IFinanceProducts — the catalogue of things sold alongside a car.
//
// Use:  inject IFinanceProducts. Deals reads it to offer products and to copy a
//       price and cost onto a deal; nothing outside this folder touches
//       FinanceProduct or the finance schema (ADR-014).
// Edit: there is no "sell this product" method here on purpose. Selling happens
//       on the deal, because the price is negotiated per deal and the sale has to
//       live or die with the deal it is on. This contract answers "what can we
//       sell and what does it usually go for", and nothing more.

using DealerFOSS.Core;

namespace DealerFOSS.Finance;

public interface IFinanceProducts
{
    Task<Result<IReadOnlyList<FinanceProductView>>> ListAsync(
        bool availableOnly,
        CancellationToken cancellationToken);

    /// <summary>
    /// A known set of products, for a screen showing what was sold on a deal.
    /// Unknown ids are simply absent.
    /// </summary>
    Task<Result<IReadOnlyList<FinanceProductView>>> GetManyAsync(
        IReadOnlyCollection<Guid> productIds,
        CancellationToken cancellationToken);

    Task<Result<FinanceProductView>> AddAsync(NewFinanceProduct product, CancellationToken cancellationToken);

    /// <summary>Moves the catalogue defaults. Never touches a deal already done.</summary>
    Task<Result<FinanceProductView>> RepriceAsync(
        Guid productId,
        decimal defaultPrice,
        decimal defaultCost,
        CancellationToken cancellationToken);

    /// <summary>
    /// Stops a product being offered without deleting it — deals already sold
    /// against it have to keep making sense.
    /// </summary>
    Task<Result<FinanceProductView>> SetAvailableAsync(
        Guid productId,
        bool available,
        CancellationToken cancellationToken);
}

public sealed record FinanceProductView(
    Guid Id,
    string Name,
    string Kind,
    string Provider,
    decimal DefaultPrice,
    decimal DefaultCost,
    string Currency,
    int? TermMonths,
    int? TermMiles,
    bool IsAvailable);

public sealed record NewFinanceProduct(
    string Name,
    string Kind,
    string Provider,
    decimal DefaultPrice,
    decimal DefaultCost,
    string Currency = "USD",
    int? TermMonths = null,
    int? TermMiles = null);
