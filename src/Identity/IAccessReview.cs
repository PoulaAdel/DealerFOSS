// Copyright (c) 2026 The DealerFOSS contributors.
// SPDX-License-Identifier: AGPL-3.0-or-later
//
// Overview: Purpose, File Design, and Engineering
//   IAccessReview — who holds which right, where, and the dealership's signature
//   that it has looked.
//
//   A NEW PUBLIC CONTRACT ON IDENTITY IS A SECURITY DECISION (ADR-017), so here
//   is the reasoning rather than a note saying one was taken. The FTC Safeguards
//   Rule obliges a dealership to review access periodically. Nothing could answer
//   "who can approve a deal" without a SQL client, so the obligation had nothing
//   behind it. Answering it means reading users, roles, permissions and
//   assignments together — four tables that are sealed inside this project on
//   purpose — so either this contract exists or those tables stop being sealed.
//   This is the smaller hole.
//
//   WHAT IT CANNOT DO, which is what makes it safe to export. It grants nothing
//   and changes no access: the only write is a signature, and a signature gives
//   nobody a permission they did not have. It carries no password hash, no TOTP
//   secret, no session, no recovery code. It names no administrator — those live
//   in ControlPlaneDb, a different context and a different schema this contract
//   never opens, and AccessReviewTests proves it rather than trusting it. And it
//   reads the whole organization or nothing: there is no per-rooftop answer to
//   be assembled into a map of who is weak where.
//
// Usage:
//   var review = await accessReview.BuildAsync(ct);         // the table
//   await accessReview.SignAsync(review.Checksum, note, me, ct);
//
// Coding Instructions:
//   ONE FLAT TABLE, NOT TWO VIEWS. A review is (person, permission, how they got
//   it, where it applies). "Everyone who can refund" and "everything Dana can
//   do" are both sorts of the same rows, so the server ships rows once and the
//   client sorts. It is also the shape that gets exported and filed, which is
//   what a periodic review actually produces.
//
//   SIGNING TAKES THE CHECKSUM THE SIGNER SAW. Not "sign the current state" —
//   somebody may have been granted something between the screen rendering and
//   the button being pressed, and a signature over a picture the signer never
//   saw is a false record. A stale checksum is refused by its own error code so
//   the screen can say what happened and re-read.

using DealerFOSS.Core;

namespace DealerFOSS.Identity;

public interface IAccessReview
{
    /// <summary>
    /// Everyone in this dealership, every permission that reaches them, and how.
    /// Includes the last signature and when the next is due.
    /// </summary>
    Task<AccessReview> BuildAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Records that somebody has reviewed the access described by
    /// <paramref name="checksum"/>.
    /// </summary>
    /// <param name="checksum">
    /// The checksum from the <see cref="AccessReview"/> the signer was looking
    /// at. Refused with <see cref="AccessReviewErrors.PictureChanged"/> if the
    /// grants have moved since — the signature must be over what they saw.
    /// </param>
    Task<Result<AccessReviewSignature>> SignAsync(
        string checksum,
        string? note,
        Guid actingUserId,
        CancellationToken cancellationToken);
}

/// <summary>
/// Who holds what, right now, and what the dealership has signed about it.
/// </summary>
/// <param name="Checksum">
/// SHA-256 of <paramref name="Rows"/>, lower-case hex. Pass it back to
/// <see cref="IAccessReview.SignAsync"/>; compare it with
/// <paramref name="LastSignature"/>'s to see whether anything has changed since
/// the dealership last looked.
/// </param>
/// <param name="NextDueAt">
/// Null when nobody has ever signed — "overdue" would be a strange thing to say
/// about a dealership that has never been asked, and "never reviewed" is the
/// more useful statement for a screen to make.
/// </param>
public sealed record AccessReview(
    DateTimeOffset TakenAt,
    IReadOnlyList<AccessReviewRow> Rows,
    string Checksum,
    AccessReviewSignature? LastSignature,
    DateTimeOffset? NextDueAt,
    int ReviewEveryDays);

/// <summary>
/// One person, one permission, and the single grant that carries it to them.
///
/// <para>
/// A person holding one permission through two roles appears twice, on purpose.
/// Collapsing them would hide that removing one role changes nothing, which is
/// exactly the thing a reviewer is trying to find out.
/// </para>
/// </summary>
public sealed record AccessReviewRow(
    Guid UserId,
    string Email,
    string DisplayName,
    bool IsActive,
    bool HasSecondFactor,
    string Permission,
    string RoleName,
    bool IsOrganizationWide,
    Guid? RooftopId,
    bool OwnRecordsOnly);

/// <summary>One completed review, as a screen or an auditor reads it.</summary>
public sealed record AccessReviewSignature(
    DateTimeOffset TakenAt,
    Guid SignedByUserId,
    string SignedByDisplayName,
    string Checksum,
    int RowCount,
    string? Note);

/// <summary>Stable error codes for the access review (doc 06 §6).</summary>
public static class AccessReviewErrors
{
    /// <summary>
    /// Its own code rather than a generic conflict, because the screen has a
    /// specific thing to do about it: say that access changed while they were
    /// reading, show the new picture, and let them sign that instead.
    /// </summary>
    public static Error PictureChanged { get; } = Error.Conflict(
        "access_review.picture_changed",
        "Somebody's access changed while you were reading this. "
        + "Check the updated list and sign that instead.");

    public static Error NothingToSign { get; } = Error.Validation(
        "access_review.nothing_to_sign",
        "Say which review you are signing.");
}
