// Copyright (c) 2026 The DealerFOSS contributors.
// SPDX-License-Identifier: AGPL-3.0-or-later
//
// Overview: Purpose, File Design, and Engineering
//   ReportingService — assembling one month from what the other capabilities know.
//
// Usage:
//   Through IReporting.
//
// Coding Instructions:
//   There is no TenantDb here, on purpose. Every figure arrives through a
//   published contract, so every permission check happens inside the
//   capability that owns the data. A dashboard that queried the tables
//   directly would be a way to read past a rooftop scope, and it would be the
//   last place anybody thought to look for one.

using DealerFOSS.Accounting;
using DealerFOSS.Core;
using DealerFOSS.Inventory;

namespace DealerFOSS.Reporting;

public sealed class ReportingService(IAccounting accounting, IInventory inventory, IClock clock)
    : IReporting
{
    private readonly IAccounting _accounting = accounting;
    private readonly IInventory _inventory = inventory;
    private readonly IClock _clock = clock;

    public async Task<Result<MonthInReview>> MonthAsync(
        MonthQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        if (query.Month is < 1 or > 12)
        {
            return Result.Failure<MonthInReview>(ReportingErrors.NotAMonth);
        }

        if (query.Year is < 2000 or > 2999)
        {
            return Result.Failure<MonthInReview>(ReportingErrors.NotAYear);
        }

        var startsOn = new DateOnly(query.Year, query.Month, 1);
        var endsOn = startsOn.AddMonths(1).AddDays(-1);
        var priorStart = startsOn.AddMonths(-1);

        var withheld = new List<string>();

        var trading = await Read(
            _accounting.PerformanceAsync(new BalanceQuery(query.RooftopId, startsOn, endsOn), cancellationToken));

        if (trading.IsFailure)
        {
            return Result.Failure<MonthInReview>(trading.Error);
        }

        // The comparison is only asked for when the month itself was allowed. It
        // is also allowed to be absent on its own account — a mixed-currency
        // month back then must not blank out this one.
        LedgerPerformance? priorMonth = null;
        if (trading.Value is not null)
        {
            var prior = await Read(
                _accounting.PerformanceAsync(
                    new BalanceQuery(query.RooftopId, priorStart, startsOn.AddDays(-1)),
                    cancellationToken));

            priorMonth = prior.IsSuccess ? prior.Value : null;
        }
        else
        {
            withheld.Add(WithheldSection.Trading);
        }

        // Stock is aged as at today when the month is the current one, and as at
        // its cutoff when it is a past month. Asking "how old was the stock" about
        // last March and being told today's ages would be a plainly wrong answer.
        var today = DateOnly.FromDateTime(_clock.UtcNow.UtcDateTime);
        var asOf = today < endsOn ? today : endsOn;

        var stock = await Read(
            _inventory.AgingAsync(new StockAgingQuery(query.RooftopId, asOf), cancellationToken));

        if (stock.IsFailure)
        {
            return Result.Failure<MonthInReview>(stock.Error);
        }

        if (stock.Value is null)
        {
            withheld.Add(WithheldSection.Stock);
        }

        // Nothing at all is a refusal, not an empty page. A dashboard with every
        // panel missing looks like a system with no data in it.
        if (withheld.Count == 2)
        {
            return Result.Failure<MonthInReview>(ReportingErrors.Forbidden);
        }

        var (books, closedAt) = await BooksAsync(query.Year, query.Month, cancellationToken);

        return Result.Success(new MonthInReview(
            query.Year,
            query.Month,
            startsOn,
            endsOn,
            books,
            closedAt,
            trading.Value,
            priorMonth,
            stock.Value,
            withheld));
    }

    /// <summary>
    /// What the books for this month are doing, or that the caller may not know.
    /// </summary>
    private async Task<(string State, DateTimeOffset? ClosedAt)> BooksAsync(
        int year,
        int month,
        CancellationToken cancellationToken)
    {
        var periods = await _accounting.ListPeriodsAsync(cancellationToken);
        if (periods.IsFailure)
        {
            return (BooksState.Unknown, null);
        }

        var period = periods.Value.FirstOrDefault(p => p.Year == year && p.Month == month);

        return period is null
            ? (BooksState.NotOpened, null)
            : (period.State, period.ClosedAt);
    }

    /// <summary>
    /// Runs one section and turns a refusal into an absence, leaving every other
    /// failure to be reported as itself.
    /// </summary>
    /// <remarks>
    /// The distinction matters: "you may not see this" is a fact about the reader
    /// and the rest of the page is still worth showing, while "the ledger holds two
    /// currencies" is a fact about the data that the reader needs to be told. Only
    /// the first one is quietly swallowed, and even then it is named in
    /// <see cref="MonthInReview.Withheld"/> rather than left blank.
    /// </remarks>
    private static async Task<Result<T?>> Read<T>(Task<Result<T>> section)
        where T : class
    {
        var result = await section;

        if (result.IsSuccess)
        {
            return Result.Success<T?>(result.Value);
        }

        return result.Error.Type == ErrorType.Forbidden
            ? Result.Success<T?>(null)
            : Result.Failure<T?>(result.Error);
    }
}

/// <summary>Stable error codes for the Reporting capability (doc 06 §6).</summary>
internal static class ReportingErrors
{
    public static Error Forbidden { get; } = Error.Forbidden(
        "reporting.forbidden",
        "You do not have access to any of the figures on this dashboard.");

    public static Error NotAMonth { get; } = Error.Validation(
        "reporting.not_a_month",
        "A month is 1 to 12.");

    public static Error NotAYear { get; } = Error.Validation(
        "reporting.not_a_year",
        "That is not a year this system reports on.");
}
