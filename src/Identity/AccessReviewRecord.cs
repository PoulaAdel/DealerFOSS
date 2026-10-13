// Copyright (c) 2026 The DealerFOSS contributors.
// SPDX-License-Identifier: AGPL-3.0-or-later
//
// Overview: Purpose, File Design, and Engineering
//   AccessReviewRecord — somebody's signature that they read who holds what on a
//   given day, and SecuritySettings — how often the dealership says that is due.
//
//   The two live together because neither means anything alone: a signature with
//   no cadence cannot be overdue, and a cadence with no signature is never met.
//
//   THE CHECKSUM IS THE POINT OF THE RECORD. An attestation saying only
//   "reviewed on the 1st" is close to worthless — if a grant changes on the 2nd,
//   nothing says whether the signer saw the picture that is there now, and
//   nobody can be shown what they actually signed off. So the signature carries
//   a SHA-256 of the review table as it stood, and "has anything changed since
//   the last review?" becomes comparing two strings rather than a judgement.
//
//   Append-only, like every other ledger in this product (ADR-016). A review
//   that was done is a fact about a day; it is never edited, and a correction is
//   another signature.
//
// Usage:
//   Written by AccessReviewService.SignAsync. Read by BuildAsync to answer "when
//   was this last reviewed, by whom, and is it due".
//
// Coding Instructions:
//   Do not add an "approved" or "findings" field without deciding what the
//   product does with it. A free-text note is here because a reviewer needs to
//   record "removed two leavers"; anything structured implies a workflow that
//   does not exist and would be a half-built one the moment it shipped.

using DealerFOSS.Core;

namespace DealerFOSS.Identity;

/// <summary>
/// One completed access review: who signed it, when, and over exactly what.
/// </summary>
internal sealed class AccessReviewRecord
{
    public Guid Id { get; private set; }

    public DateTimeOffset TakenAt { get; private set; }

    /// <summary>
    /// Who signed. Not a foreign key to Users on purpose: a review signed by
    /// somebody who has since left is still a review that happened, and the row
    /// must survive them being removed.
    /// </summary>
    public Guid SignedByUserId { get; private set; }

    /// <summary>
    /// SHA-256 of the review table, lower-case hex. What the signature is over.
    /// </summary>
    public string Checksum { get; private set; } = string.Empty;

    /// <summary>How many rows that checksum covered, so a reader can sanity-check it.</summary>
    public int RowCount { get; private set; }

    /// <summary>What the reviewer wrote, if anything. Theirs, not the system's.</summary>
    public string? Note { get; private set; }

    private AccessReviewRecord()
    {
    }

    public AccessReviewRecord(
        Guid id,
        DateTimeOffset takenAt,
        Guid signedByUserId,
        string checksum,
        int rowCount,
        string? note)
    {
        if (string.IsNullOrWhiteSpace(checksum))
        {
            throw new ArgumentException("A review is signed over a checksum.", nameof(checksum));
        }

        Id = id;
        TakenAt = takenAt;
        SignedByUserId = signedByUserId;
        Checksum = checksum;
        RowCount = rowCount;
        Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
    }
}

/// <summary>
/// The dealership's own security settings that are not per-role. One row.
/// </summary>
internal sealed class SecuritySettings : AuditableEntity
{
    /// <summary>
    /// How often an access review is due, in days.
    ///
    /// <para>
    /// Ninety by default, and the reasoning belongs with the number because
    /// somebody will ask who chose it. The FTC Safeguards Rule requires access
    /// to be reviewed <em>periodically</em> and names no interval; its own
    /// guidance is risk-based. Quarterly is the common practice, and ninety days
    /// is short enough that a leaver's access does not sit unreviewed for half a
    /// year. It is a SETTING rather than a constant because a number compiled
    /// into the product is wrong for somebody who then cannot change it.
    /// </para>
    /// </summary>
    public const int DefaultReviewEveryDays = 90;

    /// <summary>
    /// Fixed, for the reason <c>PartsSettings</c> gives for doing the same: there
    /// is exactly one of these per organization and a tenant database holds
    /// exactly one organization, so reading the setting is a primary-key lookup
    /// rather than a scan hoping for one row.
    /// </summary>
    public static readonly Guid SingletonId = new("5ec0b17e-0000-4000-8000-000000000001");

    public Guid Id { get; private set; }

    public int AccessReviewEveryDays { get; private set; } = DefaultReviewEveryDays;

    private SecuritySettings()
    {
    }

    public SecuritySettings(int everyDays)
    {
        Id = SingletonId;
        AccessReviewEveryDays = Validated(everyDays);
    }

    public void ReviewEvery(int days) => AccessReviewEveryDays = Validated(days);

    /// <summary>
    /// A cadence has to be a real interval. Zero or negative would make every
    /// review instantly overdue, and a number of years would make the setting a
    /// way of switching the obligation off quietly — which is worse than not
    /// having the setting, because the screen would still say "reviewed".
    /// </summary>
    private static int Validated(int days) =>
        days is < 1 or > 366
            ? throw new ArgumentOutOfRangeException(
                nameof(days), days, "A review is due somewhere between a day and a year.")
            : days;
}
