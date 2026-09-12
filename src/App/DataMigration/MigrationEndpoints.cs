// Copyright (c) 2026 The DealerFOSS contributors.
// SPDX-License-Identifier: AGPL-3.0-or-later
//
// Overview: Purpose, File Design, and Engineering
//   MigrationEndpoints — submitting a file and watching what happens to it.
//
// Usage:
//   POST /api/v1/migration/imports with the file's text in the body.
//
// Coding Instructions:
//   The file arrives as JSON rather than as a multipart upload because it is
//   text and the client already sends JSON with an anti-forgery header. When
//   real dealer extracts arrive — tens of megabytes — this becomes a
//   multipart or pre-signed upload, and the contract below does not change:
//   submitting still returns a job, and the job is still watched by polling.

using System.Globalization;
using System.Text;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using DealerFOSS.App;
using DealerFOSS.Core;

namespace DealerFOSS.DataMigration;

internal static class MigrationEndpoints
{
    public static void MapMigration(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/migration").WithTags("Migration");

        group.MapPost("/imports", SubmitAsync);
        group.MapGet("/imports", ListAsync);
        group.MapGet("/imports/{id:guid}", GetAsync);
        group.MapGet("/imports/{id:guid}/rows", GetRowsAsync);

        group.MapGet("/exports/{kind}", ExportAsync);
    }

    /// <summary>
    /// Returns the file itself rather than a JSON envelope around it, so a
    /// browser downloads it and `curl -O` works. The checksum rides in a header
    /// because putting it in the body would make the body not-a-CSV.
    /// </summary>
    private static async Task<IResult> ExportAsync(
        string kind,
        HttpContext context,
        IMigration migration,
        CancellationToken cancellationToken)
    {
        var result = await migration.ExportAsync(kind, cancellationToken);
        if (result.IsFailure)
        {
            return result.Error.ToProblem();
        }

        var file = result.Value;
        context.Response.Headers["X-Content-SHA256"] = file.Checksum;
        context.Response.Headers["X-Row-Count"] =
            file.RowCount.ToString(CultureInfo.InvariantCulture);

        return Results.File(
            Encoding.UTF8.GetBytes(file.Content),
            "text/csv; charset=utf-8",
            file.FileName);
    }

    private static async Task<IResult> SubmitAsync(
        SubmitImportRequest request,
        IMigration migration,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var result = await migration.SubmitAsync(
            new NewImport(request.Kind, request.Mode, request.SourceName, request.Content),
            cancellationToken);

        // 202, not 201: the rows are recorded but nothing has been imported yet.
        // Saying "created" would invite a client to believe the work is done.
        return result.IsSuccess
            ? Results.Accepted($"/api/v1/migration/imports/{result.Value.Id}", result.Value)
            : result.Error.ToProblem();
    }

    private static async Task<IResult> ListAsync(
        IMigration migration,
        CancellationToken cancellationToken,
        int limit = 25,
        int offset = 0)
    {
        var result = await migration.ListAsync(limit, offset, cancellationToken);
        return result.IsSuccess ? Results.Ok(result.Value) : result.Error.ToProblem();
    }

    private static async Task<IResult> GetAsync(
        Guid id,
        IMigration migration,
        CancellationToken cancellationToken)
    {
        var result = await migration.GetAsync(id, cancellationToken);
        return result.IsSuccess ? Results.Ok(result.Value) : result.Error.ToProblem();
    }

    private static async Task<IResult> GetRowsAsync(
        Guid id,
        IMigration migration,
        CancellationToken cancellationToken,
        bool problemsOnly = true,
        int limit = 100)
    {
        var result = await migration.GetRowsAsync(id, problemsOnly, limit, cancellationToken);
        return result.IsSuccess ? Results.Ok(result.Value) : result.Error.ToProblem();
    }
}

/// <summary>
/// A file to import. <c>Content</c> is the file's text, unaltered — it is staged
/// exactly as given, because the migration workflow forbids fixing an exception
/// by editing what the dealership sent (doc 05 §6).
/// </summary>
internal sealed record SubmitImportRequest(
    string Kind,
    string Mode,
    string SourceName,
    string Content);
