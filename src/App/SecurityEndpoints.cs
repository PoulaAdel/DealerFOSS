// Copyright (c) 2026 The DealerFOSS contributors.
// SPDX-License-Identifier: AGPL-3.0-or-later
//
// Overview: Purpose, File Design, and Engineering
//   SecurityEndpoints — the rules a dealer organization applies to its own staff.
//
// Usage:
//   GET  /api/v1/security/second-factor-policy   which roles demand one
//   POST /api/v1/security/second-factor-policy   turn it on or off for a role
//
// Coding Instructions:
//   The permission is checked here rather than inside Identity, because
//   Identity answers "what may this user reach?" and must not also decide
//   who may change the answer — that is circular. The check demands
//   organization-wide scope: a rule about the whole dealership is not set
//   from one lot.

using System.Globalization;
using System.Text;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using DealerFOSS.Core;
using DealerFOSS.Identity;

namespace DealerFOSS.App;

internal static class SecurityEndpoints
{
    public static void MapSecurity(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/security").WithTags("Security");

        group.MapGet("/second-factor-policy", GetSecondFactorPolicyAsync);
        group.MapPost("/second-factor-policy", SetSecondFactorPolicyAsync);

        // Who holds what, and the dealership's signature that it has looked.
        // Its own permission rather than Staff.Read — see
        // Permissions.SecurityReviewAccess for why that one is not enough.
        group.MapGet("/access-review", GetAccessReviewAsync);
        group.MapGet("/access-review/export", ExportAccessReviewAsync);
        group.MapPost("/access-review/sign", SignAccessReviewAsync);

        // How often the review is due. Changing it is managing policy, not
        // reviewing — a reviewer must not be able to make themselves on time by
        // moving the date.
        group.MapPost("/access-review/interval", SetReviewIntervalAsync);
    }

    private static async Task<IResult> GetAccessReviewAsync(
        ICurrentUser currentUser,
        IAccessDirectory access,
        IAccessReview review,
        CancellationToken cancellationToken)
    {
        if (!await MayReviewAsync(currentUser, access, cancellationToken))
        {
            return ReviewRefused.ToProblem();
        }

        return Results.Ok(await review.BuildAsync(cancellationToken));
    }

    /// <summary>
    /// The same table as a file, because a periodic review produces a record
    /// that gets filed rather than a screen somebody looked at. The checksum
    /// rides in a header for the same reason the records-package export does it:
    /// putting it in the body would make the body not-a-CSV.
    /// </summary>
    private static async Task<IResult> ExportAccessReviewAsync(
        HttpContext context,
        ICurrentUser currentUser,
        IAccessDirectory access,
        IAccessReview review,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (!await MayReviewAsync(currentUser, access, cancellationToken))
        {
            return ReviewRefused.ToProblem();
        }

        var built = await review.BuildAsync(cancellationToken);
        var content = AccessReviewCsv.Render(built);

        context.Response.Headers["X-Content-SHA256"] = built.Checksum;
        context.Response.Headers["X-Row-Count"] =
            built.Rows.Count.ToString(CultureInfo.InvariantCulture);

        return Results.File(
            Encoding.UTF8.GetBytes(content),
            "text/csv; charset=utf-8",
            $"access-review-{built.TakenAt:yyyy-MM-dd}.csv");
    }

    private static async Task<IResult> SignAccessReviewAsync(
        SignAccessReviewRequest request,
        ICurrentUser currentUser,
        IAccessDirectory access,
        IAccessReview review,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!await MayReviewAsync(currentUser, access, cancellationToken))
        {
            return ReviewRefused.ToProblem();
        }

        var result = await review.SignAsync(
            request.Checksum, request.Note, currentUser.Id, cancellationToken);

        return result.IsSuccess ? Results.Ok(result.Value) : result.Error.ToProblem();
    }

    private static async Task<IResult> SetReviewIntervalAsync(
        ReviewIntervalRequest request,
        ICurrentUser currentUser,
        IAccessDirectory access,
        ISecurityPolicy policy,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!await MayManageAsync(currentUser, access, cancellationToken))
        {
            return Refused.ToProblem();
        }

        var result = await policy.ReviewAccessEveryAsync(
            request.EveryDays, currentUser.Id, cancellationToken);

        return result.IsSuccess ? Results.NoContent() : result.Error.ToProblem();
    }

    /// <summary>
    /// Organization-wide, like managing the policy and for a sharper reason: a
    /// review narrowed to one lot would silently omit everybody who can reach
    /// every lot, which is precisely the group a reviewer most needs to see.
    /// </summary>
    private static async Task<bool> MayReviewAsync(
        ICurrentUser currentUser,
        IAccessDirectory access,
        CancellationToken cancellationToken)
    {
        var scope = await access.GetAuthorizedScopeAsync(
            currentUser.Id, Permissions.SecurityReviewAccess, cancellationToken);

        return scope.IsOrganizationWide;
    }

    private static async Task<IResult> GetSecondFactorPolicyAsync(
        ICurrentUser currentUser,
        IAccessDirectory access,
        ISecurityPolicy policy,
        CancellationToken cancellationToken)
    {
        if (!await MayManageAsync(currentUser, access, cancellationToken))
        {
            return Refused.ToProblem();
        }

        return Results.Ok(await policy.ListSecondFactorPolicyAsync(cancellationToken));
    }

    private static async Task<IResult> SetSecondFactorPolicyAsync(
        SecondFactorPolicyRequest request,
        ICurrentUser currentUser,
        IAccessDirectory access,
        ISecurityPolicy policy,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!await MayManageAsync(currentUser, access, cancellationToken))
        {
            return Refused.ToProblem();
        }

        var result = await policy.RequireSecondFactorAsync(
            request.RoleId, request.Required, currentUser.Id, cancellationToken);

        return result.IsSuccess ? Results.NoContent() : result.Error.ToProblem();
    }

    /// <summary>
    /// Organization-wide, not "covers some rooftop". Holding the permission at
    /// one lot must not let somebody change what the whole group has to do.
    /// </summary>
    private static async Task<bool> MayManageAsync(
        ICurrentUser currentUser,
        IAccessDirectory access,
        CancellationToken cancellationToken)
    {
        var scope = await access.GetAuthorizedScopeAsync(
            currentUser.Id, Permissions.SecurityManagePolicy, cancellationToken);

        return scope.IsOrganizationWide;
    }

    private static Error Refused { get; } = Error.Forbidden(
        "security.policy_forbidden",
        "Changing the dealership's security policy needs organization-wide permission.");

    private static Error ReviewRefused { get; } = Error.Forbidden(
        "security.review_forbidden",
        "Reviewing who has access needs organization-wide permission.");
}

/// <summary>What a caller signs, and anything they want recorded with it.</summary>
internal sealed record SignAccessReviewRequest(string Checksum, string? Note = null);

/// <summary>How often a review falls due.</summary>
internal sealed record ReviewIntervalRequest(int EveryDays);

/// <summary>
/// The review table as a CSV.
///
/// <para>
/// This writes its own quoting rather than reusing <c>Exporter.Line</c> in
/// DataMigration, and the duplication is deliberate:
/// <c>FeatureBoundaryTests.A_feature_must_not_reach_into_another_feature</c>
/// forbids a capability reaching into a sibling, and an access review borrowing
/// the migration capability's internals to save ten lines would be exactly that.
/// The alternative — moving a CSV writer somewhere shared — is a bigger change
/// than this file justifies on its own, and would be the right one the day a
/// third capability needs it.
/// </para>
/// </summary>
internal static class AccessReviewCsv
{
    private static readonly string[] Columns =
    [
        "email", "name", "active", "second_factor",
        "permission", "role", "organization_wide", "rooftop_id", "own_records_only",
    ];

    public static string Render(AccessReview review)
    {
        ArgumentNullException.ThrowIfNull(review);

        var text = new StringBuilder();
        text.AppendLine(Line(Columns));

        foreach (var row in review.Rows)
        {
            text.AppendLine(Line([
                row.Email,
                row.DisplayName,
                row.IsActive ? "yes" : "no",
                row.HasSecondFactor ? "yes" : "no",
                row.Permission,
                row.RoleName,
                row.IsOrganizationWide ? "yes" : "no",

                // Empty for an organization-wide grant rather than a zero Guid: a
                // reader scanning the column wants "every lot" to look different
                // from "a lot", not like a lot whose id is all noughts.
                row.RooftopId?.ToString() ?? string.Empty,
                row.OwnRecordsOnly ? "yes" : "no",
            ]));
        }

        return text.ToString();
    }

    /// <summary>
    /// Every field quoted, always. A dealership's role names and display names
    /// are typed by people and will eventually contain a comma, a quote or a
    /// newline; quoting unconditionally costs nothing and removes the question.
    /// </summary>
    private static string Line(string[] fields)
    {
        var line = new StringBuilder();

        for (var i = 0; i < fields.Length; i++)
        {
            if (i > 0)
            {
                line.Append(',');
            }

            line.Append('"')
                .Append(fields[i].Replace("\"", "\"\"", StringComparison.Ordinal))
                .Append('"');
        }

        return line.ToString();
    }
}

/// <summary>Turn the second-factor obligation on or off for one role.</summary>
internal sealed record SecondFactorPolicyRequest(Guid RoleId, bool Required);
