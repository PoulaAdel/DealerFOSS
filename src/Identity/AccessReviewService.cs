// Copyright (c) 2026 The DealerFOSS contributors.
// SPDX-License-Identifier: AGPL-3.0-or-later
//
// Overview: Purpose, File Design, and Engineering
//   AccessReviewService — flattens users, roles, permissions and assignments
//   into the one table a reviewer reads, and records a signature over it.
//
//   NO PERMISSION CHECK LIVES HERE. This is Identity answering a question it was
//   asked; whether the asker may ask it is decided by the endpoint against
//   Security.ReviewAccess, organization-wide, exactly as every other capability
//   decides its own. Putting the check here as well would be a second copy of an
//   authorization rule, which is the thing this project does not do.
//
// Usage:
//   Registered by AddIdentity(). Reached only through IAccessReview.
//
// Coding Instructions:
//   THE CHECKSUM MUST NOT DEPEND ON ROW ORDER OR ON TODAY. It is sorted before
//   hashing, and the timestamp is deliberately outside it — otherwise every read
//   would produce a new checksum, every signature would be instantly stale, and
//   the comparison that makes the whole record worth keeping would always say
//   "changed".
//
//   Loaded in three queries rather than joined in one. The three sets are small
//   — a dealership has tens of people, a handful of roles — and the cross
//   product is built in memory where it is readable. If a group ever has
//   thousands of staff this becomes a join; until then, clarity wins and the
//   query count is not the thing that would be slow.

using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using DealerFOSS.Core;

namespace DealerFOSS.Identity;

internal sealed class AccessReviewService(IdentityDb db, IClock clock) : IAccessReview
{
    private readonly IdentityDb _db = db;
    private readonly IClock _clock = clock;

    public async Task<AccessReview> BuildAsync(CancellationToken cancellationToken)
    {
        // Users in THIS dealership's identity schema. Administrators are not
        // absent because they are filtered out — they are in ControlPlaneDb,
        // which this context cannot see. Said here because "they do not appear"
        // should read as a structural fact rather than a filter somebody could
        // later forget (doc 06 §2).
        var users = await _db.Users
            .AsNoTracking()
            .OrderBy(u => u.Email)
            .ToListAsync(cancellationToken);

        var roles = await _db.Roles
            .AsNoTracking()
            .Include(r => r.Permissions)
            .ToListAsync(cancellationToken);

        var assignments = await _db.UserAssignments
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        var byRole = roles.ToDictionary(r => r.Id);

        var rows = new List<AccessReviewRow>();

        foreach (var user in users)
        {
            foreach (var assignment in assignments.Where(a => a.UserId == user.Id))
            {
                if (!byRole.TryGetValue(assignment.RoleId, out var role))
                {
                    // A grant naming a role that no longer exists reaches nobody,
                    // so it is not access and does not belong in a list of who
                    // has access. It is also not nothing — it is a stale row — so
                    // it is skipped here rather than rendered as a permission
                    // called "(unknown)" that a reviewer would then chase.
                    continue;
                }

                foreach (var permission in role.Permissions.Select(p => p.Permission))
                {
                    rows.Add(new AccessReviewRow(
                        user.Id,
                        user.Email,
                        user.DisplayName,
                        user.IsActive,
                        user.MfaEnabled,
                        permission,
                        role.Name,
                        assignment.Scope == AssignmentScope.Organization,
                        assignment.RooftopId?.Value,
                        assignment.OwnRecordsOnly));
                }
            }
        }

        rows = [.. rows
            .OrderBy(r => r.Email, StringComparer.Ordinal)
            .ThenBy(r => r.Permission, StringComparer.Ordinal)
            .ThenBy(r => r.RoleName, StringComparer.Ordinal)
            .ThenBy(r => r.RooftopId)];

        var checksum = Fingerprint(rows);

        var last = await LastSignatureAsync(users, cancellationToken);

        var settings = await _db.SecuritySettings
            .AsNoTracking()
            .SingleOrDefaultAsync(s => s.Id == SecuritySettings.SingletonId, cancellationToken);

        var everyDays = settings?.AccessReviewEveryDays ?? SecuritySettings.DefaultReviewEveryDays;

        return new AccessReview(
            _clock.UtcNow,
            rows,
            checksum,
            last,
            // Null rather than "overdue since the dawn of time" for a dealership
            // nobody has ever reviewed: the honest statement is "never reviewed",
            // and a date in 2019 would read as a missed deadline that never was.
            last is null ? null : last.TakenAt.AddDays(everyDays),
            everyDays);
    }

    public async Task<Result<AccessReviewSignature>> SignAsync(
        string checksum,
        string? note,
        Guid actingUserId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(checksum))
        {
            return Result.Failure<AccessReviewSignature>(AccessReviewErrors.NothingToSign);
        }

        // Rebuilt rather than trusted. The signature has to be over what is
        // actually true now AND over what the signer saw; the only way to know
        // both is to compute it again and compare.
        var current = await BuildAsync(cancellationToken);

        if (!string.Equals(current.Checksum, checksum.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            return Result.Failure<AccessReviewSignature>(AccessReviewErrors.PictureChanged);
        }

        var record = new AccessReviewRecord(
            Guid.NewGuid(), _clock.UtcNow, actingUserId, current.Checksum, current.Rows.Count, note);

        _db.AccessReviews.Add(record);
        await _db.SaveChangesAsync(cancellationToken);

        var signer = await _db.Users
            .AsNoTracking()
            .SingleOrDefaultAsync(u => u.Id == actingUserId, cancellationToken);

        return Result.Success(new AccessReviewSignature(
            record.TakenAt,
            actingUserId,
            signer?.DisplayName ?? string.Empty,
            record.Checksum,
            record.RowCount,
            record.Note));
    }

    private async Task<AccessReviewSignature?> LastSignatureAsync(
        List<User> users,
        CancellationToken cancellationToken)
    {
        var record = await _db.AccessReviews
            .AsNoTracking()
            .OrderByDescending(r => r.TakenAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (record is null)
        {
            return null;
        }

        // Empty rather than "(deleted)": a review signed by somebody who has
        // since left is still a review that happened, and the row deliberately
        // does not point at a user that has to still exist.
        var name = users.SingleOrDefault(u => u.Id == record.SignedByUserId)?.DisplayName ?? string.Empty;

        return new AccessReviewSignature(
            record.TakenAt, record.SignedByUserId, name, record.Checksum, record.RowCount, record.Note);
    }

    /// <summary>
    /// A stable fingerprint of who holds what.
    ///
    /// <para>
    /// Over the rows only — not the time it was read, which changes on every
    /// call and would make every signature stale the instant it was written.
    /// Field-separated with a character that cannot occur in any of the values,
    /// so two different tables cannot produce one string: without it, a role
    /// named "A|B" and two roles "A" and "B" would hash alike.
    /// </para>
    /// </summary>
    private static string Fingerprint(IReadOnlyList<AccessReviewRow> rows)
    {
        var text = new StringBuilder();

        foreach (var row in rows)
        {
            text.Append(CultureInfo.InvariantCulture, $"{row.UserId:D}\u001f");
            text.Append(CultureInfo.InvariantCulture, $"{row.Permission}\u001f");
            text.Append(CultureInfo.InvariantCulture, $"{row.RoleName}\u001f");
            text.Append(CultureInfo.InvariantCulture, $"{row.IsOrganizationWide}\u001f");
            text.Append(CultureInfo.InvariantCulture, $"{row.RooftopId:D}\u001f");
            text.Append(CultureInfo.InvariantCulture, $"{row.OwnRecordsOnly}\u001f");

            // Active and second-factor are IN the fingerprint on purpose. A
            // leaver being deactivated changes who effectively has access, and a
            // review signed before that is a review of a different dealership.
            text.Append(CultureInfo.InvariantCulture, $"{row.IsActive}\u001f");
            text.Append(CultureInfo.InvariantCulture, $"{row.HasSecondFactor}\u001e");
        }

        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text.ToString())))
            .ToLowerInvariant();
    }
}
