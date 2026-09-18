// Copyright (c) 2026 The DealerFOSS contributors.
// SPDX-License-Identifier: AGPL-3.0-or-later
//
// Overview: Purpose, File Design, and Engineering
//   IAccessDirectory — the Identity module's public contract, and the only part of
//   it other modules may reference (ADR-008).
//
// Usage:
//   Ask "what may this user reach?" (GetAuthorizedScopeAsync) or "may they
//   reach this rooftop?" (IsAuthorizedAsync). Never read assignments yourself.
//
// Coding Instructions:
//   Changing a signature here is a cross-module break — every caller must be
//   updated in the same change. An empty AuthorizedScope means DENY; keep
//   that contract, or callers will read it as "no filter".
//
//   GetHeldPermissionsAsync was added on 2026-09-18 and is the one method here
//   that DOES NOT MAKE A DECISION. It exists so a screen can stop offering a
//   door that will answer 403, and it must never be the thing that closes the
//   door. Read its own remarks before calling it.

using DealerFOSS.Core;

namespace DealerFOSS.Identity;

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

    /// <summary>
    /// Every permission the user holds <em>somewhere</em> — organization-wide,
    /// or on at least one rooftop. Empty for an inactive user.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>This answers "what should we OFFER", never "what may they DO".</b>
    /// It deliberately throws away the scope: a person who may read accounting
    /// at one rooftop out of four appears here identically to one who may read
    /// it everywhere, because the question it serves is whether to draw a
    /// navigation link at all. Anything that needs to know <em>where</em> must
    /// call <see cref="GetAuthorizedScopeAsync"/>, and anything deciding
    /// whether an act is allowed must call that or
    /// <see cref="IsAuthorizedAsync"/>.
    /// </para>
    /// <para>
    /// Nothing on the server may branch on this. It exists because the browser
    /// had no way to know what the caller holds, so every signed-in person was
    /// shown every module and found out by being refused — a technician saw
    /// Books and Staff. Hiding a link is a courtesy; the endpoint behind it
    /// still refuses, and <c>PermissionsAreAHintNotAControl</c> in the
    /// integration tests is what keeps that true.
    /// </para>
    /// <para>
    /// Returning this to the caller leaks nothing: it is a list of what that
    /// person could already discover by clicking on their own screen.
    /// </para>
    /// </remarks>
    Task<IReadOnlySet<string>> GetHeldPermissionsAsync(
        Guid userId,
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
