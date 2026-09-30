// Copyright (c) 2026 The DealerFOSS contributors.
// SPDX-License-Identifier: AGPL-3.0-or-later
//
// Overview: Purpose, File Design, and Engineering
//   UserAssignment — grants a user a role, either across the whole organization or
//   at one named rooftop. The same user may hold different roles at different
//   rooftops (doc 04 §1).
//
// Usage:
//   UserAssignment.ForOrganization(...) or .ForRooftop(...).
//
// Coding Instructions:
//   Keep organization-wide as its own scope rather than "a list of every
//   rooftop". A rooftop opened next year must be covered automatically by
//   the first and deliberately not by the second.

using DealerFOSS.Core;

namespace DealerFOSS.Identity;

/// <summary>
/// Grants a user a role at a scope: either organization-wide, or at one named
/// rooftop. A user may hold different roles at different rooftops (doc 04 §1).
/// </summary>
internal sealed class UserAssignment : AuditableEntity
{
    public Guid Id { get; private set; }

    public Guid UserId { get; private set; }

    public Guid RoleId { get; private set; }

    public AssignmentScope Scope { get; private set; }

    /// <summary>Set when <see cref="Scope"/> is <see cref="AssignmentScope.Rooftop"/>; otherwise null.</summary>
    public RooftopId? RooftopId { get; private set; }

    /// <summary>
    /// Narrows this grant to records the person is named on — their own deals,
    /// their own jobs (ADR-030). A narrowing of the scope above rather than a
    /// level below it: it says nothing about WHICH lot, only about which records
    /// within whatever this grant already reaches.
    ///
    /// Defaults false, so every assignment made before this existed keeps exactly
    /// the reach it had. Taking access away is the direction that needs granting
    /// deliberately, not the direction a migration takes on somebody's behalf.
    /// </summary>
    public bool OwnRecordsOnly { get; private set; }

    private UserAssignment()
    {
    }

    private UserAssignment(
        Guid id,
        Guid userId,
        Guid roleId,
        AssignmentScope scope,
        RooftopId? rooftopId,
        bool ownRecordsOnly)
    {
        Id = id;
        UserId = userId;
        RoleId = roleId;
        Scope = scope;
        RooftopId = rooftopId;
        OwnRecordsOnly = ownRecordsOnly;
    }

    /// <summary>Grants the role across every rooftop in the organization.</summary>
    public static UserAssignment ForOrganization(
        Guid id, Guid userId, Guid roleId, bool ownRecordsOnly = false) =>
        new(id, userId, roleId, AssignmentScope.Organization, rooftopId: null, ownRecordsOnly);

    /// <summary>Grants the role at exactly one rooftop.</summary>
    public static UserAssignment ForRooftop(
        Guid id, Guid userId, Guid roleId, RooftopId rooftopId, bool ownRecordsOnly = false) =>
        new(id, userId, roleId, AssignmentScope.Rooftop, rooftopId, ownRecordsOnly);

    /// <summary>Whether this assignment covers the given rooftop.</summary>
    public bool Covers(RooftopId rooftopId) =>
        Scope == AssignmentScope.Organization || RooftopId == rooftopId;
}
