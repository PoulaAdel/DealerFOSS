// IOrganization — this module's public contract, and the only part of
// it other modules may reference (ADR-008).
//
// Use:  ask for the organization structure, one rooftop, or whether a rooftop
//       exists. Reads are already scoped to the caller's rooftops.
// Edit: changing a signature here is a cross-module break. Do not add an
//       "unscoped" overload — that is how a leak gets introduced politely.

using DealerFOSS.Core;

namespace DealerFOSS.Organization;

/// <summary>
/// The Organization module's public contract — the only surface other modules
/// may call (ADR-008). Reads are scoped to the current user's authorized
/// rooftops; callers cannot opt out of that check.
/// </summary>
public interface IOrganization
{
    /// <summary>
    /// The current tenant's organization, containing only the rooftops the
    /// caller may read. Fails with a forbidden error when the caller has no
    /// covering assignment.
    /// </summary>
    Task<Result<OrganizationView>> GetStructureAsync(CancellationToken cancellationToken);

    /// <summary>
    /// One rooftop, if the caller is authorized for it. An unauthorized rooftop
    /// and an unknown rooftop return the same failure, so the response does not
    /// reveal which rooftops exist.
    /// </summary>
    Task<Result<RooftopView>> GetRooftopAsync(RooftopId rooftopId, CancellationToken cancellationToken);

    /// <summary>
    /// Whether a rooftop exists in the current tenant. Existence only — this is
    /// for other modules validating a reference, and grants no data access.
    /// </summary>
    Task<bool> RooftopExistsAsync(RooftopId rooftopId, CancellationToken cancellationToken);
}
