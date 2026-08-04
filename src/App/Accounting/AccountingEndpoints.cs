// AccountingEndpoints — the HTTP surface for the ledger.
//
// Use:  mapped from Program.cs; routes under /api/v1/accounting.
//       GET  /api/v1/accounting/accounts
//       GET  /api/v1/accounting/journal?rooftopId=&reference=&from=&to=
//       GET  /api/v1/accounting/journal/{id}
//       POST /api/v1/accounting/journal/{id}/reverse
// Edit: there is no endpoint that creates an entry. Entries are the consequence
//       of business events — a delivery posts one — and a general-purpose
//       posting endpoint would be a hole straight through the controls.

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
        group.MapGet("/journal", ListAsync);
        group.MapGet("/journal/{entryId:guid}", GetAsync);
        group.MapPost("/journal/{entryId:guid}/reverse", ReverseAsync);
    }

    private static async Task<IResult> ListAccountsAsync(
        IAccounting accounting,
        CancellationToken cancellationToken)
    {
        var result = await accounting.ListAccountsAsync(cancellationToken);
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
