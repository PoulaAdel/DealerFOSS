// OrganizationView and friends — the read models this module hands out.
//
// Use:  returned by IOrganizationDirectory and serialized directly to clients.
// Edit: these are a published API shape. Entities never cross the boundary;
//       map into these instead. Removing or renaming a field breaks callers, so
//       treat additions as the safe change and removals as versioned ones.

using OpenDealer360.Core;

namespace OpenDealer360.Organization.Contracts;

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
    IReadOnlyList<DepartmentView> Departments);

public sealed record DepartmentView(
    DepartmentId Id,
    string Name,
    string Kind);
