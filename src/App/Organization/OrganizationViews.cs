// Copyright (c) 2026 The DealerFOSS contributors.
// SPDX-License-Identifier: AGPL-3.0-or-later
//
// Overview: Purpose, File Design, and Engineering
//   OrganizationView and friends — the read models this module hands out.
//
// Usage:
//   Returned by IOrganization and serialized directly to clients.
//
// Coding Instructions:
//   These are a published API shape. Entities never cross the boundary;
//   map into these instead. Removing or renaming a field breaks callers, so
//   treat additions as the safe change and removals as versioned ones.

using DealerFOSS.Core;

namespace DealerFOSS.Organization;

/// <summary>
/// Read models the Organization module exposes across module and API boundaries.
/// Entities never leave the module; these projections do (doc 08 §5).
/// </summary>
public sealed record OrganizationView(
    DealerOrganizationId Id,
    string Name,
    string Slug,
    IReadOnlyList<LegalEntityView> LegalEntities);

public sealed record LegalEntityView(
    LegalEntityId Id,
    string Name,
    IReadOnlyList<RooftopView> Rooftops);

public sealed record RooftopView(
    RooftopId Id,
    string Name,
    string Code,
    string TimeZone,
    IReadOnlyList<DepartmentView> Departments)
{
    /// <summary>
    /// Which legal entity owns this rooftop. Money belongs to a legal entity, not
    /// to a building, so anything posting an accounting entry needs this — and it
    /// cannot be added retrospectively to entries that are already immutable.
    /// </summary>
    public LegalEntityId LegalEntityId { get; init; }
}

public sealed record DepartmentView(
    DepartmentId Id,
    string Name,
    string Kind);
