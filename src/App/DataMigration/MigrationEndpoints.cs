// MigrationEndpoints — submitting a file and watching what happens to it.
//
// Use:  POST /api/v1/migration/imports with the file's text in the body.
// Edit: the file arrives as JSON rather than as a multipart upload because it is
//       text and the client already sends JSON with an anti-forgery header. When
//       real dealer extracts arrive — tens of megabytes — this becomes a
//       multipart or pre-signed upload, and the contract below does not change:
//       submitting still returns a job, and the job is still watched by polling.

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using OpenDealer360.App;
using OpenDealer360.Core;

namespace OpenDealer360.DataMigration;

internal static class MigrationEndpoints
{
    public static void MapMigration(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/migration").WithTags("Migration");

        group.MapPost("/imports", SubmitAsync);
        group.MapGet("/imports", ListAsync);
        group.MapGet("/imports/{id:guid}", GetAsync);
        group.MapGet("/imports/{id:guid}/rows", GetRowsAsync);
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
        int limit = 25)
    {
        var result = await migration.ListAsync(limit, cancellationToken);
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
