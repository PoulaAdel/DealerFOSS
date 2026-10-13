// Copyright (c) 2026 The DealerFOSS contributors.
// SPDX-License-Identifier: AGPL-3.0-or-later
//
// Overview: Purpose, File Design, and Engineering
//   ISecurityPolicy — reading and changing the security rules a dealer organization
//   applies to its own staff.
//
// Usage:
//   List the roles and their second-factor obligation; set one. The caller is
//   responsible for checking the permission first — this contract does what
//   it is told and records who told it.
//
// Coding Instructions:
//   This is the third public surface of the Identity project, and it exists
//   because a policy nobody can change is not a policy. Keep it to policy:
//   creating users, granting roles, and reading assignments stay internal
//   (ADR-017), and adding them here would be a different decision entirely.

using DealerFOSS.Core;

namespace DealerFOSS.Identity;

/// <summary>
/// The security policy a dealer organization applies to its own staff. Today
/// that is one rule: which roles must hold a second factor. It lives in the
/// tenant's own database, so one organization's decision never reaches another.
/// </summary>
public interface ISecurityPolicy
{
    /// <summary>Every role in this organization, and whether it demands a second factor.</summary>
    Task<IReadOnlyList<RoleSecondFactorPolicy>> ListSecondFactorPolicyAsync(
        CancellationToken cancellationToken);

    /// <summary>
    /// Turns the obligation on or off for one role, recording who changed it.
    /// Fails if the role does not exist rather than silently doing nothing.
    /// </summary>
    Task<Result> RequireSecondFactorAsync(
        Guid roleId,
        bool required,
        Guid actingUserId,
        CancellationToken cancellationToken);

    /// <summary>
    /// How many days may pass between access reviews before the next one is due.
    /// </summary>
    Task<int> AccessReviewEveryDaysAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Changes that interval, recording who changed it. Refused outside one day
    /// to a year — zero would make every review instantly overdue, and a decade
    /// would be a way of switching the obligation off while the screen still
    /// said "reviewed".
    /// </summary>
    Task<Result> ReviewAccessEveryAsync(
        int days,
        Guid actingUserId,
        CancellationToken cancellationToken);
}

/// <summary>One role's second-factor obligation, and how many people it affects.</summary>
public sealed record RoleSecondFactorPolicy(
    Guid RoleId,
    string RoleName,
    bool RequiresSecondFactor,
    int UsersHolding,
    int UsersStillToEnrol);
