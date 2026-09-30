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
    /// <remarks>
    /// Use this for an act with no record behind it yet — receiving a car,
    /// opening a job, booking one in. Anything that reaches an EXISTING record
    /// that names somebody should use the overload below and pass who, or a
    /// caller whose grant reaches only their own work will be let through to
    /// somebody else's record (ADR-030).
    /// </remarks>
    Task<bool> IsAuthorizedAsync(
        Guid userId,
        string permission,
        RooftopId rooftopId,
        CancellationToken cancellationToken);

    /// <summary>
    /// Whether the user may exercise <paramref name="permission"/> on one record:
    /// the rooftop it sits at, and whether it is theirs when their grant goes no
    /// wider than their own work. Denials are audited, and the audit reason tells
    /// the two refusals apart even though the caller is told neither.
    /// </summary>
    /// <param name="ownerUserId">
    /// Whoever the record names — the salesperson on a deal, the technician on a
    /// job, the advisor on a booking. <c>null</c> means nobody is named, which is
    /// refused for a caller limited to their own work rather than treated as
    /// everybody's.
    /// </param>
    Task<bool> IsAuthorizedAsync(
        Guid userId,
        string permission,
        RooftopId rooftopId,
        Guid? ownerUserId,
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
/// <remarks>
/// This answers about a RECORD, not only about a rooftop (ADR-030). It carries
/// the caller's own id so a service never has to hand back an identity the scope
/// was already resolved for, and it is the one place a later dimension is added
/// — that is the whole reason it exists in this shape.
/// </remarks>
public sealed record AuthorizedScope(
    bool IsOrganizationWide,
    IReadOnlySet<RooftopId> Rooftops,
    bool OwnRecordsOnly = false,
    Guid UserId = default)
{
    public static AuthorizedScope None { get; } =
        new(false, new HashSet<RooftopId>());

    public static AuthorizedScope OrganizationWide { get; } =
        new(true, new HashSet<RooftopId>());

    public bool GrantsNothing => !IsOrganizationWide && Rooftops.Count == 0;

    /// <summary>
    /// May this caller reach the lot at all.
    ///
    /// Kept alongside <see cref="Allows"/> deliberately. Receiving stock, opening
    /// a job and booking a car in all happen before there is a record with an
    /// owner, so they have a real question to ask and no owner to ask it with.
    /// Collapsing the two would force those sites to invent one.
    /// </summary>
    public bool Covers(RooftopId rooftopId) => IsOrganizationWide || Rooftops.Contains(rooftopId);

    /// <summary>
    /// May this caller reach this record: the lot, and then whether the record is
    /// theirs when their grant goes no wider than their own work.
    /// </summary>
    /// <param name="ownerUserId">
    /// Whoever the record names — the salesperson on a deal, the technician on a
    /// job. <c>null</c> means nobody is named, and an unowned record is NOT
    /// everybody's: a caller limited to their own work is refused it. That is the
    /// safe direction, and where a capability genuinely means "unclaimed, so
    /// anyone may take it" — an enquiry in the pool — it says so itself with
    /// <see cref="Covers"/> rather than having that meaning assumed here for
    /// every record in the product.
    /// </param>
    public bool Allows(RooftopId rooftopId, Guid? ownerUserId) =>
        Covers(rooftopId)
        && (!OwnRecordsOnly || (ownerUserId is { } owner && owner == UserId));
}
