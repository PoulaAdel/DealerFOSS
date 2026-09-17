// Copyright (c) 2026 The DealerFOSS contributors.
// SPDX-License-Identifier: AGPL-3.0-or-later
//
// Overview: Purpose, File Design, and Engineering
//   ReceivableEndpoints — who owes the dealership money, and recording it
//   arriving.
//
// Usage:
//   GET  /api/v1/receivables                          who still owes us
//   GET  /api/v1/receivables/{id}                     one account and its payments
//   GET  /api/v1/receivables/for/{source}/{reference} the debt against one bill
//   POST /api/v1/receivables/{id}/payments            record money arriving
//   GET  /api/v1/receivables/credits                  what we owe customers back
//   POST /api/v1/receivables/credits/{id}/apply       put a credit against a bill
//   POST /api/v1/receivables/credits/{id}/refund      hand a credit back
//   GET  /api/v1/receivables/ageing                   who owes what, by how overdue
//   GET  /api/v1/receivables/statement/{customerId}   one customer's account over a period
//
// Coding Instructions:
//   Keep it thin — delegate, then map a Result to a status code. The rooftop
//   scope is applied in ReceivableService, not here.
//
//   There is deliberately NO endpoint that opens a receivable. A debt is a
//   consequence of delivering a car or invoicing a job, and both do it inside
//   their own transaction. A way to type one in by hand would be a way to have
//   somebody owe money nobody billed them for.

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using DealerFOSS.App;
using DealerFOSS.Core;

namespace DealerFOSS.Receivables;

internal static class ReceivableEndpoints
{
    public static void MapReceivables(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/receivables").WithTags("Receivables");

        group.MapGet("", ListAsync);

        // Before the {id} route, so "for", "credits", "ageing" and "statement"
        // are not offered to the guid constraint as candidate ids.
        group.MapGet("/for/{source}/{reference}", FindAsync);
        group.MapGet("/credits", ListCreditsAsync);
        group.MapPost("/credits/{creditId:guid}/apply", ApplyCreditAsync);
        group.MapPost("/credits/{creditId:guid}/refund", RefundCreditAsync);
        group.MapGet("/ageing", GetAgeingAsync);
        group.MapGet("/statement/{customerId:guid}", GetStatementAsync);
        group.MapGet("/{receivableId:guid}", GetAsync);
        group.MapPost("/{receivableId:guid}/payments", PayAsync);
    }

    private static async Task<IResult> ListAsync(
        IReceivables receivables,
        Guid? rooftopId,
        Guid? customerId,
        bool? outstandingOnly,
        int? limit,
        int? offset,
        CancellationToken cancellationToken)
    {
        var result = await receivables.ListAsync(
            new ReceivableQuery(
                rooftopId is { } id ? new RooftopId(id) : null,
                customerId,
                outstandingOnly ?? true,
                limit ?? 50,
                offset ?? 0),
            cancellationToken);

        return result.IsSuccess ? Results.Ok(result.Value) : result.Error.ToProblem();
    }

    private static async Task<IResult> GetAsync(
        IReceivables receivables,
        Guid receivableId,
        CancellationToken cancellationToken)
    {
        var result = await receivables.GetAsync(receivableId, cancellationToken);

        return result.IsSuccess ? Results.Ok(result.Value) : result.Error.ToProblem();
    }

    private static async Task<IResult> FindAsync(
        IReceivables receivables,
        string source,
        string reference,
        CancellationToken cancellationToken)
    {
        if (!Enum.TryParse<ReceivableSource>(source, ignoreCase: true, out var parsed))
        {
            return Error
                .Validation("receivables.unknown_source", "That is not something this system bills for.")
                .ToProblem();
        }

        var result = await receivables.FindAsync(parsed, reference, cancellationToken);
        if (result.IsFailure)
        {
            return result.Error.ToProblem();
        }

        // A bill nobody owes anything against is a legitimate answer, not a
        // missing page: a deal still being worked has no receivable yet.
        return result.Value is null ? Results.NoContent() : Results.Ok(result.Value);
    }

    private static async Task<IResult> ListCreditsAsync(
        IReceivables receivables,
        Guid? rooftopId,
        Guid? customerId,
        bool? openOnly,
        int? limit,
        int? offset,
        CancellationToken cancellationToken)
    {
        var result = await receivables.ListCreditsAsync(
            new CreditQuery(
                rooftopId is { } id ? new RooftopId(id) : null,
                customerId,
                openOnly ?? true,
                limit ?? 50,
                offset ?? 0),
            cancellationToken);

        return result.IsSuccess ? Results.Ok(result.Value) : result.Error.ToProblem();
    }

    private static async Task<IResult> ApplyCreditAsync(
        IReceivables receivables,
        Guid creditId,
        ApplyCredit application,
        CancellationToken cancellationToken)
    {
        var result = await receivables.ApplyCreditAsync(creditId, application, cancellationToken);

        return result.IsSuccess ? Results.Ok(result.Value) : result.Error.ToProblem();
    }

    private static async Task<IResult> RefundCreditAsync(
        IReceivables receivables,
        Guid creditId,
        RefundCredit refund,
        CancellationToken cancellationToken)
    {
        var result = await receivables.RefundCreditAsync(creditId, refund, cancellationToken);

        return result.IsSuccess ? Results.Ok(result.Value) : result.Error.ToProblem();
    }

    private static async Task<IResult> GetAgeingAsync(
        IReceivables receivables,
        Guid? rooftopId,
        CancellationToken cancellationToken)
    {
        var result = await receivables.GetAgeingAsync(
            new AgeingQuery(rooftopId is { } id ? new RooftopId(id) : null),
            cancellationToken);

        return result.IsSuccess ? Results.Ok(result.Value) : result.Error.ToProblem();
    }

    private static async Task<IResult> GetStatementAsync(
        IReceivables receivables,
        Guid customerId,
        DateTimeOffset from,
        DateTimeOffset to,
        Guid? rooftopId,
        CancellationToken cancellationToken)
    {
        var result = await receivables.GetStatementAsync(
            new StatementQuery(customerId, from, to, rooftopId is { } id ? new RooftopId(id) : null),
            cancellationToken);

        return result.IsSuccess ? Results.Ok(result.Value) : result.Error.ToProblem();
    }

    private static async Task<IResult> PayAsync(
        IReceivables receivables,
        Guid receivableId,
        NewPayment payment,
        CancellationToken cancellationToken)
    {
        var result = await receivables.RecordPaymentAsync(receivableId, payment, cancellationToken);

        return result.IsSuccess ? Results.Ok(result.Value) : result.Error.ToProblem();
    }
}
