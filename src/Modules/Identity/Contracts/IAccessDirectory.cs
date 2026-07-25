using OpenDealer360.Platform.Kernel;

namespace OpenDealer360.Modules.Identity.Contracts;

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
