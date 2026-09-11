// Copyright (c) 2026 The DealerFOSS contributors.
// SPDX-License-Identifier: AGPL-3.0-or-later
//
// Overview: Purpose, File Design, and Engineering
//   AccountingEndpoints — the HTTP surface for the ledger.
//
// Usage:
//   Mapped from Program.cs; routes under /api/v1/accounting.
//   GET  /api/v1/accounting/accounts
//   GET  /api/v1/accounting/performance?rooftopId=&from=&to=
//   GET  /api/v1/accounting/journal?rooftopId=&reference=&from=&to=
//   GET  /api/v1/accounting/journal/{id}
//   POST /api/v1/accounting/journal/{id}/reverse
//
// Coding Instructions:
//   There is no endpoint that creates an entry. Entries are the consequence
//   of business events — a delivery posts one — and a general-purpose
//   posting endpoint would be a hole straight through the controls.

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using DealerFOSS.App;
using DealerFOSS.Core;

namespace DealerFOSS.Accounting;

internal static class AccountingEndpoints
{
    public static void MapAccounting(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/accounting").WithTags("Accounting");

        group.MapGet("/accounts", ListAccountsAsync);
        group.MapGet("/balances", TrialBalanceAsync);
        group.MapGet("/performance", PerformanceAsync);
        group.MapGet("/profit-and-loss", ProfitAndLossAsync);
        group.MapGet("/balance-sheet", BalanceSheetAsync);
        group.MapGet("/journal", ListAsync);
        group.MapGet("/journal/{entryId:guid}", GetAsync);
        group.MapPost("/journal", PostManualAsync);
        group.MapPost("/journal/{entryId:guid}/reverse", ReverseAsync);

        // Opening, closing, and reopening a month. Separate paths rather than one
        // "set the state" endpoint, because reopening needs a different
        // permission and a reason, and a single endpoint would blur that.
        group.MapGet("/periods", ListPeriodsAsync);
        group.MapPost("/periods", OpenPeriodAsync);
        group.MapPost("/periods/{year:int}/{month:int}/close", ClosePeriodAsync);
        group.MapPost("/periods/{year:int}/{month:int}/reopen", ReopenPeriodAsync);
    }

    private static async Task<IResult> ListAccountsAsync(
        IAccounting accounting,
        CancellationToken cancellationToken)
    {
        var result = await accounting.ListAccountsAsync(cancellationToken);
        return result.IsSuccess ? Results.Ok(result.Value) : result.Error.ToProblem();
    }

    private static async Task<IResult> ListPeriodsAsync(
        IAccounting accounting,
        CancellationToken cancellationToken)
    {
        var result = await accounting.ListPeriodsAsync(cancellationToken);
        return result.IsSuccess ? Results.Ok(result.Value) : result.Error.ToProblem();
    }

    private static async Task<IResult> OpenPeriodAsync(
        OpenPeriodRequest request,
        IAccounting accounting,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var result = await accounting.OpenPeriodAsync(
            request.Year, request.Month, request.Note, cancellationToken);

        return result.IsSuccess ? Results.Ok(result.Value) : result.Error.ToProblem();
    }

    private static async Task<IResult> ClosePeriodAsync(
        int year,
        int month,
        PeriodNoteRequest? request,
        IAccounting accounting,
        CancellationToken cancellationToken)
    {
        var result = await accounting.ClosePeriodAsync(year, month, request?.Note, cancellationToken);
        return result.IsSuccess ? Results.Ok(result.Value) : result.Error.ToProblem();
    }

    private static async Task<IResult> ReopenPeriodAsync(
        int year,
        int month,
        PeriodNoteRequest request,
        IAccounting accounting,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var result = await accounting.ReopenPeriodAsync(
            year, month, request.Note ?? string.Empty, cancellationToken);

        return result.IsSuccess ? Results.Ok(result.Value) : result.Error.ToProblem();
    }

    private static async Task<IResult> TrialBalanceAsync(
        IAccounting accounting,
        CancellationToken cancellationToken,
        Guid? rooftopId = null,
        DateOnly? from = null,
        DateOnly? to = null)
    {
        var query = new BalanceQuery(
            rooftopId is null ? null : new RooftopId(rooftopId.Value), from, to);

        var result = await accounting.TrialBalanceAsync(query, cancellationToken);
        return result.IsSuccess ? Results.Ok(result.Value) : result.Error.ToProblem();
    }

    private static async Task<IResult> PerformanceAsync(
        IAccounting accounting,
        CancellationToken cancellationToken,
        Guid? rooftopId = null,
        DateOnly? from = null,
        DateOnly? to = null)
    {
        var query = new BalanceQuery(
            rooftopId is null ? null : new RooftopId(rooftopId.Value), from, to);

        var result = await accounting.PerformanceAsync(query, cancellationToken);
        return result.IsSuccess ? Results.Ok(result.Value) : result.Error.ToProblem();
    }


    private static async Task<IResult> ProfitAndLossAsync(
        IAccounting accounting,
        CancellationToken cancellationToken,
        Guid? rooftopId = null,
        DateOnly? from = null,
        DateOnly? to = null)
    {
        var query = new BalanceQuery(
            rooftopId is null ? null : new RooftopId(rooftopId.Value), from, to);

        var result = await accounting.ProfitAndLossAsync(query, cancellationToken);
        return result.IsSuccess ? Results.Ok(result.Value) : result.Error.ToProblem();
    }

    private static async Task<IResult> BalanceSheetAsync(
        IAccounting accounting,
        CancellationToken cancellationToken,
        Guid? rooftopId = null,
        DateOnly? asAt = null)
    {
        // No `from`: a balance sheet is a position, not a period. Offering one
        // would invite a caller to ask for a month and get arithmetic nonsense.
        var query = new BalanceQuery(
            rooftopId is null ? null : new RooftopId(rooftopId.Value), null, asAt);

        var result = await accounting.BalanceSheetAsync(query, cancellationToken);
        return result.IsSuccess ? Results.Ok(result.Value) : result.Error.ToProblem();
    }

    private static async Task<IResult> PostManualAsync(
        IAccounting accounting,
        ManualEntryRequest request,
        CancellationToken cancellationToken)
    {
        var result = await accounting.PostManualAsync(
            new ManualPosting(
                new RooftopId(request.RooftopId),
                request.EntryDate,
                request.Memo,
                request.Currency,
                request.Lines),
            cancellationToken);

        return result.IsSuccess ? Results.Ok(result.Value) : result.Error.ToProblem();
    }

    /// <summary>
    /// The wire shape for a hand-written entry. Separate from
    /// <see cref="ManualPosting"/> only because the rooftop arrives as a plain
    /// Guid over HTTP.
    /// </summary>
    internal sealed record ManualEntryRequest(
        Guid RooftopId,
        DateOnly EntryDate,
        string Memo,
        string Currency,
        IReadOnlyList<ManualLine> Lines);

    private static async Task<IResult> ListAsync(
        IAccounting accounting,
        CancellationToken cancellationToken,
        Guid? rooftopId = null,
        string? reference = null,
        DateOnly? from = null,
        DateOnly? to = null,
        int limit = 50)
    {
        var query = new JournalQuery(
            rooftopId is null ? null : new RooftopId(rooftopId.Value),
            reference,
            from,
            to,
            limit);

        var result = await accounting.ListAsync(query, cancellationToken);
        return result.IsSuccess ? Results.Ok(result.Value) : result.Error.ToProblem();
    }

    private static async Task<IResult> GetAsync(
        Guid entryId,
        IAccounting accounting,
        CancellationToken cancellationToken)
    {
        var result = await accounting.GetAsync(entryId, cancellationToken);
        return result.IsSuccess ? Results.Ok(result.Value) : result.Error.ToProblem();
    }

    private static async Task<IResult> ReverseAsync(
        Guid entryId,
        ReverseRequest request,
        IAccounting accounting,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var result = await accounting.ReverseAsync(entryId, request.Reason, cancellationToken);
        return result.IsSuccess ? Results.Ok(result.Value) : result.Error.ToProblem();
    }
}

/// <summary>A reversal always states why. An unexplained one is a mistake.</summary>
internal sealed record ReverseRequest(string Reason);

internal sealed record OpenPeriodRequest(int Year, int Month, string? Note = null);

/// <summary>
/// Optional when closing, required when reopening — the service enforces that,
/// because "why is a reported month being unlocked" is the question the history
/// exists to answer.
/// </summary>
internal sealed record PeriodNoteRequest(string? Note = null);
