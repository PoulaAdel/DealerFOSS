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

        if (assignments.Exists(a => a.Scope == AssignmentScope.Organization))
        {
            return AuthorizedScope.OrganizationWide;
        }

        var rooftops = assignments
            .Where(a => a.RooftopId is not null)
            .Select(a => a.RooftopId!.Value)
            .ToHashSet();

        return new AuthorizedScope(false, rooftops);
    }

    public async Task<bool> IsAuthorizedAsync(
        Guid userId,
        string permission,
        RooftopId rooftopId,
        CancellationToken cancellationToken)
    {
        var scope = await GetAuthorizedScopeAsync(userId, permission, cancellationToken);
        if (scope.Covers(rooftopId))
        {
            return true;
        }

        await _audit.RecordAsync(
            AuditEntry.Denied(
                actorUserId: userId,
                action: permission,
                resourceType: "Rooftop",
                resourceId: rooftopId.ToString(),
                rooftopId: rooftopId.Value,
                reason: "User holds no assignment covering this rooftop for this permission."),
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
