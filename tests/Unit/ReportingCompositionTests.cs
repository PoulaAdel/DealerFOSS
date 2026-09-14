// Copyright (c) 2026 The DealerFOSS contributors.
// SPDX-License-Identifier: AGPL-3.0-or-later
//
// Overview: Purpose, File Design, and Engineering
//   ReportingCompositionTests — the rules ReportingService applies when one of the
//   capabilities it composes says no.
//
// Usage:
//   Runs with the normal test suite; no database.
//
// Coding Instructions:
//   The valuable assertions here are the three about failure. A dashboard
//   that turns every refusal into a blank panel is lying by omission, and a
//   dashboard that turns a data problem into a blank panel is hiding a
//   problem somebody needs to fix. The difference between those two is the
//   whole of this class.

using FluentAssertions;
using DealerFOSS.Accounting;
using DealerFOSS.Core;
using DealerFOSS.Inventory;
using DealerFOSS.Reporting;
using Xunit;

namespace DealerFOSS.UnitTests;

public sealed class ReportingCompositionTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 7, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task A_month_carries_its_cutoff_and_the_comparison_with_the_month_before()
    {
        var accounting = new StubAccounting();
        var service = new ReportingService(accounting, new StubInventory(), new FixedClock(Now));

        var review = (await service.MonthAsync(new MonthQuery(2026, 8), default)).Value;

        review.StartsOn.Should().Be(new DateOnly(2026, 8, 1));
        review.EndsOn.Should().Be(new DateOnly(2026, 8, 31), because: "August has 31 days");
        review.Trading.Should().NotBeNull();
        review.PriorMonth.Should().NotBeNull();
        review.Withheld.Should().BeEmpty();

        accounting.Asked.Should().BeEquivalentTo(
            [
                (new DateOnly(2026, 8, 1), new DateOnly(2026, 8, 31)),
                (new DateOnly(2026, 7, 1), new DateOnly(2026, 7, 31)),
            ],
            because: "the comparison is the same query over the previous month");
    }

    [Fact]
    public async Task February_ends_on_the_day_February_actually_ends()
    {
        var service = new ReportingService(new StubAccounting(), new StubInventory(), new FixedClock(Now));

        var review = (await service.MonthAsync(new MonthQuery(2024, 2), default)).Value;

        review.EndsOn.Should().Be(new DateOnly(2024, 2, 29), because: "2024 is a leap year");
    }

    [Fact]
    public async Task A_refused_section_becomes_an_absence_that_says_its_own_name()
    {
        // The case a salesperson lands in: stock is theirs to see, the money is not.
        var service = new ReportingService(
            new StubAccounting { Refuse = true }, new StubInventory(), new FixedClock(Now));

        var result = await service.MonthAsync(new MonthQuery(2026, 8), default);

        result.IsSuccess.Should().BeTrue(because: "the half they may read is still worth reading");
        result.Value.Trading.Should().BeNull();
        result.Value.Stock.Should().NotBeNull();
        result.Value.Withheld.Should().Equal(WithheldSection.Trading);
        result.Value.Books.Should().Be(BooksState.Unknown,
            because: "somebody who may not read the ledger may not know what its months are doing");
    }

    [Fact]
    public async Task A_failure_that_is_not_a_refusal_is_reported_rather_than_swallowed()
    {
        // Two currencies in the ledger is a fact about the data, and the person
        // looking at the dashboard is exactly who needs to be told. Turning this
        // into a blank panel would hide a problem behind a permission-shaped hole.
        var accounting = new StubAccounting
        {
            Failure = Error.Validation("accounting.mixed_currencies", "Two currencies."),
        };

        var result = await new ReportingService(accounting, new StubInventory(), new FixedClock(Now))
            .MonthAsync(new MonthQuery(2026, 8), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("accounting.mixed_currencies");
    }

    [Fact]
    public async Task A_caller_who_may_see_nothing_is_refused_rather_than_shown_an_empty_page()
    {
        var result = await new ReportingService(
                new StubAccounting { Refuse = true },
                new StubInventory { Refuse = true },
                new FixedClock(Now))
            .MonthAsync(new MonthQuery(2026, 8), default);

        result.IsFailure.Should().BeTrue(
            because: "an empty dashboard must never be how a refusal looks");
        result.Error.Type.Should().Be(ErrorType.Forbidden);
        result.Error.Code.Should().Be("reporting.forbidden");
    }

    [Fact]
    public async Task Stock_in_the_current_month_is_aged_as_at_today()
    {
        var inventory = new StubInventory();

        await new ReportingService(new StubAccounting(), inventory, new FixedClock(Now))
            .MonthAsync(new MonthQuery(2026, 8), default);

        inventory.AsOf.Should().Be(new DateOnly(2026, 8, 7), because: "the month has not finished");
    }

    [Fact]
    public async Task Stock_in_a_past_month_is_aged_as_at_that_months_cutoff()
    {
        // Being shown today's ages when you asked about last March would be a
        // plainly wrong answer to a question somebody asked in good faith.
        var inventory = new StubInventory();

        await new ReportingService(new StubAccounting(), inventory, new FixedClock(Now))
            .MonthAsync(new MonthQuery(2026, 3), default);

        inventory.AsOf.Should().Be(new DateOnly(2026, 3, 31));
    }

    [Theory]
    [InlineData(2026, 0)]
    [InlineData(2026, 13)]
    [InlineData(1999, 6)]
    public async Task A_month_that_does_not_exist_is_refused(int year, int month)
    {
        var result = await new ReportingService(
                new StubAccounting(), new StubInventory(), new FixedClock(Now))
            .MonthAsync(new MonthQuery(year, month), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Validation);
    }

    [Fact]
    public async Task A_month_nobody_has_opened_says_so_rather_than_reading_as_open()
    {
        var service = new ReportingService(
            new StubAccounting(), new StubInventory(), new FixedClock(Now));

        // The stub only knows about 2026-08.
        var review = (await service.MonthAsync(new MonthQuery(2026, 5), default)).Value;

        review.Books.Should().Be(BooksState.NotOpened);
    }

    // --- stubs ---------------------------------------------------------------
    //
    // Hand written rather than mocked: what is being tested is which calls happen
    // and what is done with the answers, and recording that in a few fields is
    // clearer than a chain of set-ups.

    private sealed class FixedClock(DateTimeOffset now) : IClock
    {
        public DateTimeOffset UtcNow { get; } = now;
    }

    private sealed class StubAccounting : IAccounting
    {
        /// <summary>Every period this was asked to total, in order.</summary>
        public List<(DateOnly? From, DateOnly? To)> Asked { get; } = [];

        public bool Refuse { get; init; }

        public Error? Failure { get; init; }

        public Task<Result<LedgerPerformance>> PerformanceAsync(
            BalanceQuery query,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(query);
            Asked.Add((query.From, query.To));

            if (Failure is { } failure)
            {
                return Task.FromResult(Result.Failure<LedgerPerformance>(failure));
            }

            if (Refuse)
            {
                return Task.FromResult(Result.Failure<LedgerPerformance>(
                    Error.Forbidden("accounting.forbidden", "No.")));
            }

            return Task.FromResult(Result.Success(new LedgerPerformance(
                query.From, query.To, "USD", [], 0m, 0m, 0m, 0, 0)));
        }

        public Task<Result<IReadOnlyList<AccountingPeriodView>>> ListPeriodsAsync(
            CancellationToken cancellationToken)
        {
            if (Refuse)
            {
                return Task.FromResult(Result.Failure<IReadOnlyList<AccountingPeriodView>>(
                    Error.Forbidden("accounting.forbidden", "No.")));
            }

            return Task.FromResult(Result.Success<IReadOnlyList<AccountingPeriodView>>(
            [
                new AccountingPeriodView(
                    Guid.NewGuid(), 2026, 8, "Open",
                    new DateOnly(2026, 8, 1), new DateOnly(2026, 8, 31), null, null, 0, []),
            ]));
        }

        public Task<Result<IReadOnlyList<AccountView>>> ListAccountsAsync(CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<Result<Page<JournalEntrySummary>>> ListAsync(
            JournalQuery query, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<Result<JournalEntryDetail>> GetAsync(Guid entryId, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<Result<TrialBalance>> TrialBalanceAsync(
            BalanceQuery query, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<Result<JournalEntryDetail>> PostDeliveryAsync(
            DeliveryPosting delivery, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<Result<JournalEntryDetail>> PostServiceInvoiceAsync(
            ServiceInvoicePosting invoice, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<Result<JournalEntryDetail>> PostStockPurchaseAsync(
            StockPurchasePosting purchase, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<Result<JournalEntryDetail>> PostPaymentAsync(
            PaymentPosting payment, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<Result<JournalEntryDetail>> PostCreditApplicationAsync(
            CreditApplicationPosting application, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<Result<JournalEntryDetail>> PostCreditRefundAsync(
            CreditRefundPosting refund, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<Result<ProfitAndLoss>> ProfitAndLossAsync(
            BalanceQuery query, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<Result<BalanceSheet>> BalanceSheetAsync(
            BalanceQuery query, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<Result<JournalEntryDetail>> PostManualAsync(
            ManualPosting entry, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<Result<JournalEntryDetail>> ReverseAsync(
            Guid entryId, string reason, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<Result<AccountingPeriodView>> OpenPeriodAsync(
            int year, int month, string? note, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<Result<AccountingPeriodView>> ClosePeriodAsync(
            int year, int month, string? note, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<Result<AccountingPeriodView>> ReopenPeriodAsync(
            int year, int month, string reason, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }

    private sealed class StubInventory : IInventory
    {
        /// <summary>The day the stock was asked to be aged at.</summary>
        public DateOnly? AsOf { get; private set; }

        public bool Refuse { get; init; }

        /// <summary>Reporting never asks this; the dashboard does not care about stock ownership.</summary>
        public Task<Result<Guid?>> FindOwnedAsync(
            Guid vehicleId, RooftopId rooftopId, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<Result<StockAging>> AgingAsync(
            StockAgingQuery query,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(query);
            AsOf = query.AsOf;

            if (Refuse)
            {
                return Task.FromResult(Result.Failure<StockAging>(
                    Error.Forbidden("inventory.forbidden", "No.")));
            }

            return Task.FromResult(Result.Success(
                new StockAging(query.AsOf ?? default, 0, [], [])));
        }

        public Task<Result<Page<InventoryUnitSummary>>> ListAsync(
            InventoryQuery query, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<Result<InventoryUnitDetail>> GetAsync(Guid unitId, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<Result<IReadOnlyList<InventoryUnitSummary>>> GetManyAsync(
            IReadOnlyCollection<Guid> unitIds, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<Result<InventoryUnitDetail>> ReceiveAsync(
            NewInventoryUnit unit, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<Result<InventoryUnitDetail>> ChangeStatusAsync(
            Guid unitId, StatusChangeRequest change, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }
}
