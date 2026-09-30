// Copyright (c) 2026 The DealerFOSS contributors.
// SPDX-License-Identifier: AGPL-3.0-or-later
//
// Overview: Purpose, File Design, and Engineering
//   AccessService — decides what a user may reach, and records every refusal.
//
// Usage:
//   Through IAccessDirectory; do not construct it directly.
//
// Coding Instructions:
//   Deny is the default and must stay so — an unknown user, an inactive
//   user, or one with no covering assignment gets AuthorizedScope.None.
//   If you add a fast path, make sure it cannot turn "no rows" into access.

using Microsoft.EntityFrameworkCore;
using DealerFOSS.Identity;
using DealerFOSS.Core;

namespace DealerFOSS.Identity;

/// <summary>
/// Resolves what a user may reach, and records denials. Deny is the default:
/// an inactive user, an unknown user, or a user with no covering assignment
/// gets <see cref="AuthorizedScope.None"/> rather than unfiltered access.
/// </summary>
internal sealed class AccessService(IdentityDb db, IAuditSink audit) : IAccessDirectory
{
    private readonly IdentityDb _db = db;
    private readonly IAuditSink _audit = audit;

    public async Task<AuthorizedScope> GetAuthorizedScopeAsync(
        Guid userId,
        string permission,
        CancellationToken cancellationToken)
    {
        var assignments = await LoadCoveringAssignmentsAsync(userId, permission, cancellationToken);

        if (assignments.Count == 0)
        {
            return AuthorizedScope.None;
        }

        // Limited to their own work only when EVERY grant that reaches this
        // permission says so (ADR-030). Grants are additive here — one
        // organization-wide assignment already beats a set of rooftops — and the
        // same rule has to hold for this or adding a second, narrower role would
        // silently take away reach the first one gave. `All` on a non-empty list,
        // so a single ordinary grant is enough to be unrestricted.
        var ownRecordsOnly = assignments.TrueForAll(a => a.OwnRecordsOnly);

        if (assignments.Exists(a => a.Scope == AssignmentScope.Organization))
        {
            return new AuthorizedScope(true, new HashSet<RooftopId>(), ownRecordsOnly, userId);
        }

        var rooftops = assignments
            .Where(a => a.RooftopId is not null)
            .Select(a => a.RooftopId!.Value)
            .ToHashSet();

        return new AuthorizedScope(false, rooftops, ownRecordsOnly, userId);
    }

    public Task<bool> IsAuthorizedAsync(
        Guid userId,
        string permission,
        RooftopId rooftopId,
        CancellationToken cancellationToken) =>
        AuthorizeAsync(userId, permission, rooftopId, ownerUserId: null,
            askedAboutARecord: false, cancellationToken);

    public Task<bool> IsAuthorizedAsync(
        Guid userId,
        string permission,
        RooftopId rooftopId,
        Guid? ownerUserId,
        CancellationToken cancellationToken) =>
        AuthorizeAsync(userId, permission, rooftopId, ownerUserId,
            askedAboutARecord: true, cancellationToken);

    private async Task<bool> AuthorizeAsync(
        Guid userId,
        string permission,
        RooftopId rooftopId,
        Guid? ownerUserId,
        bool askedAboutARecord,
        CancellationToken cancellationToken)
    {
        var scope = await GetAuthorizedScopeAsync(userId, permission, cancellationToken);

        // WHICH OVERLOAD WAS CALLED decides which question is asked, and nothing
        // else does. `askedAboutARecord` is false only when the caller reached
        // the three-argument overload above, which passes no owner because there
        // is no record yet — starting a deal, opening a job, booking a car in.
        //
        // The first version of this inferred the question instead: "no owner AND
        // an unrestricted scope means the lot, otherwise the record." That looks
        // equivalent and is not. For a caller limited to their own work it sent
        // every rooftop-level act down the record path with a null owner, so they
        // were refused their own FIRST deal — there is no salesperson on a deal
        // that does not exist yet. Rehearsed: OwnRecordsOnlyTests goes red on
        // `Their_own_deal_is_theirs_to_read_and_to_work_on` if this is inferred
        // rather than passed.
        var allowed = askedAboutARecord
            ? scope.Allows(rooftopId, ownerUserId)
            : scope.Covers(rooftopId);

        if (allowed)
        {
            return true;
        }

        // The reason distinguishes the two refusals, because a manager reading
        // the audit trail needs to tell "wrong lot" from "not their record" —
        // the first is somebody in the wrong place, the second is somebody doing
        // their job at the edge of it. The CALLER is told neither: both answer
        // with the capability's one Forbidden error.
        var reason = scope.Covers(rooftopId)
            ? "User's grant reaches only records they are named on, and this is not one."
            : "User holds no assignment covering this rooftop for this permission.";

        await _audit.RecordAsync(
            AuditEntry.Denied(
                actorUserId: userId,
                action: permission,
                resourceType: "Rooftop",
                resourceId: rooftopId.ToString(),
                rooftopId: rooftopId.Value,
                reason: reason),
            cancellationToken);

        return false;
    }

    /// <summary>
    /// Every permission this user holds somewhere. See the remarks on
    /// <see cref="IAccessDirectory.GetHeldPermissionsAsync"/> — this decides
    /// nothing, and nothing on the server may branch on it.
    /// </summary>
    public async Task<IReadOnlySet<string>> GetHeldPermissionsAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        // The same active-user gate the scope query uses. A suspended account
        // whose assignments are still on the record holds nothing, and it would
        // be a poor joke to draw them a full navigation on the way out.
        var isActive = await _db.Users
            .AsNoTracking()
            .AnyAsync(u => u.Id == userId && u.IsActive, cancellationToken);

        if (!isActive)
        {
            return new HashSet<string>();
        }

        // One round trip. The obvious alternative — calling
        // GetAuthorizedScopeAsync once per permission — is thirty-three queries
        // on every page load, and it would also need a list of every permission
        // maintained by hand beside the one in Permissions.
        var held = await _db.UserAssignments
            .AsNoTracking()
            .Where(a => a.UserId == userId)
            .Join(_db.Roles, a => a.RoleId, r => r.Id, (_, r) => r)
            .SelectMany(r => r.Permissions.Select(p => p.Permission))
            .Distinct()
            .ToListAsync(cancellationToken);

        return held.ToHashSet(StringComparer.Ordinal);
    }

    /// <summary>
    /// Assignments held by an active user whose role grants the permission.
    /// </summary>
    private async Task<List<UserAssignment>> LoadCoveringAssignmentsAsync(
        Guid userId,
        string permission,
        CancellationToken cancellationToken)
    {
        var isActive = await _db.Users
            .AsNoTracking()
            .AnyAsync(u => u.Id == userId && u.IsActive, cancellationToken);

        if (!isActive)
        {
            return [];
        }

        var grantingRoleIds = _db.Roles
            .AsNoTracking()
            .Where(r => r.Permissions.Any(p => p.Permission == permission))
            .Select(r => r.Id);

        return await _db.UserAssignments
            .AsNoTracking()
            .Where(a => a.UserId == userId && grantingRoleIds.Contains(a.RoleId))
            .ToListAsync(cancellationToken);
    }
}
