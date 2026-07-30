// IAccessDirectory — the Identity module's public contract, and the only part of
// it other modules may reference (ADR-008).
//
// Use:  ask "what may this user reach?" (GetAuthorizedScopeAsync) or "may they
//       reach this rooftop?" (IsAuthorizedAsync). Never read assignments yourself.
// Edit: changing a signature here is a cross-module break — every caller must be
//       updated in the same change. An empty AuthorizedScope means DENY; keep
//       that contract, or callers will read it as "no filter".

using OpenDealer360.Core;

namespace OpenDealer360.Identity;

/// <summary>
/// The Identity module's public contract — the only surface other modules may
/// call (ADR-008). Modules ask it for access decisions instead of reading
/// assignments themselves, so scope rules live in exactly one place.
/// </summary>
public interface IAccessDirectory
{
    /// <summary>
    /// The rooftops the user may exercise <paramref name="permission"/> on.
    /// Empty means no access — callers must treat that as a denial, never as
    /// "unfiltered".
    /// </summary>
    Task<AuthorizedScope> GetAuthorizedScopeAsync(
        Guid userId,
        string permission,
        CancellationToken cancellationToken);

    /// <summary>
    /// Whether the user may exercise <paramref name="permission"/> on one
    /// rooftop. Denials are audited by the implementation.
    /// </summary>
    Task<bool> IsAuthorizedAsync(
        Guid userId,
        string permission,
        RooftopId rooftopId,
        CancellationToken cancellationToken);
}

/// <summary>
/// What a user may reach for a given permission. Organization-wide access is
/// represented explicitly rather than as "every rooftop currently known", so a
/// rooftop added later is covered without re-granting.
/// </summary>
public sealed record AuthorizedScope(bool IsOrganizationWide, IReadOnlySet<RooftopId> Rooftops)
{
    public static AuthorizedScope None { get; } =
        new(false, new HashSet<RooftopId>());

    public static AuthorizedScope OrganizationWide { get; } =
        new(true, new HashSet<RooftopId>());

    public bool GrantsNothing => !IsOrganizationWide && Rooftops.Count == 0;

    public bool Covers(RooftopId rooftopId) => IsOrganizationWide || Rooftops.Contains(rooftopId);
}
