// Copyright (c) 2026 The DealerFOSS contributors.
// SPDX-License-Identifier: AGPL-3.0-or-later
//
// Overview: Purpose, File Design, and Engineering
//   One page of a list, and the two numbers that decide which page it is.
//
//   THIS IS ONE TYPE ON PURPOSE. Every list in the application returns the same
//   shape, so the browser has one contract to read, one component to render it,
//   and one place where "what does total mean" is answered. The alternative —
//   LeadPage, DealPage, PartPage — is the same record written ten times, and a
//   shape written ten times is a shape that will be wrong in one of them.
//
//   A LIMIT WITHOUT AN OFFSET IS A DEAD END, and that is the defect this type
//   exists to stop recurring. Nine lists took a limit, clamped it, and returned
//   the first N rows with no way to ask for the next N. The screens said
//   "showing the first 50" honestly and offered nothing. A dealership with 400
//   cars in stock could not see car 51.
//
// Usage:
//   Task<Result<Page<InventoryUnitSummary>>> ListAsync(...)
//
//   var take = Paging.Limit(query.Limit, fallback: 50);
//   var skip = Paging.Offset(query.Offset);
//   var total = await rows.CountAsync(cancellationToken);
//   var page  = await rows.OrderBy(...).Skip(skip).Take(take).ToListAsync(...);
//   return Result.Success(new Page<T>(page, total, skip, take));
//
// Coding Instructions:
//   COUNT OVER THE SAME FILTERS AS THE PAGE, and count before taking it. A total
//   that ignores the filter is worse than no total: it tells the reader there
//   are more rows and then hands them an empty page when they ask for those.
//
//   ORDER BEFORE SKIP, ALWAYS, and make the order part of the query rather than
//   something the screen does afterwards. Skipping an unordered set is
//   undefined: the database may return row 51 twice and row 52 never. The
//   enquiry list learned this the expensive way — see LeadOrder.
//
//   Offset paging, not keyset. It is what a page-numbered screen needs, it is
//   what every list here already half-implemented, and the row counts involved
//   are a dealership's, not a social network's. Revisit only when a list is
//   genuinely deep enough for the skip to cost something measurable.

namespace DealerFOSS.Core;

/// <summary>
/// One page of rows, with enough context to say where it sits in the whole.
/// </summary>
/// <typeparam name="T">The row type, as the list shows it.</typeparam>
/// <param name="Rows">The rows on this page. Never null; empty past the end.</param>
/// <param name="Total">
/// How many rows match the query in total, counted over the SAME filters as the
/// page. This is the point of a page rather than a list: "showing the first 50,
/// there may be more" is true and useless, and a dealership needs to know
/// whether it is 51 or 5,100.
/// </param>
/// <param name="Offset">How many rows were skipped to reach this page.</param>
/// <param name="Limit">The page size actually used, after clamping.</param>
public sealed record Page<T>(IReadOnlyList<T> Rows, int Total, int Offset, int Limit)
{
    /// <summary>Whether asking for the next page would return anything.</summary>
    public bool HasMore => Offset + Rows.Count < Total;
}

/// <summary>
/// Clamping for the two numbers a caller sends. Shared so that every list
/// refuses the same nonsense in the same way.
/// </summary>
public static class Paging
{
    /// <summary>What a caller gets when it asks for no particular page size.</summary>
    public const int DefaultLimit = 50;

    /// <summary>
    /// The most rows any one request may have, whatever it asks for.
    /// </summary>
    /// <remarks>
    /// A cap rather than a courtesy: without it a caller can ask for every row a
    /// dealership has ever written and the request becomes a way to make the
    /// database do arbitrary work. It is enforced here rather than trusted to
    /// each service, because nine of them had their own copy of this number and
    /// two of those copies disagreed.
    /// </remarks>
    public const int MaxLimit = 200;

    /// <summary>
    /// How many rows to take, clamped. Zero and negatives mean "unspecified" and
    /// get <paramref name="fallback"/>, because a caller that omits the
    /// parameter and a caller that sends 0 want the same thing.
    /// </summary>
    public static int Limit(int requested, int fallback = DefaultLimit, int max = MaxLimit) =>
        Math.Clamp(requested <= 0 ? fallback : requested, 1, max);

    /// <summary>
    /// How many rows to skip. A negative offset is treated as the first page
    /// rather than refused: it is a caller bug with an obvious safe reading, and
    /// failing the request would only turn a mildly wrong page into an error
    /// screen.
    /// </summary>
    public static int Offset(int requested) => Math.Max(0, requested);
}
