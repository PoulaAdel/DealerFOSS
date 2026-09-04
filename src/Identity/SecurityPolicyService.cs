// Copyright (c) 2026 The DealerFOSS contributors.
// SPDX-License-Identifier: AGPL-3.0-or-later
//
// Overview: Purpose, File Design, and Engineering
//   SecurityPolicyService — reads and changes the second-factor obligation.
//
// Usage:
//   Through ISecurityPolicy; the application never touches Role directly.
//
// Coding Instructions:
//   The counts returned by the listing are the point, not decoration. An
//   administrator about to demand a second factor of forty people needs to
//   know that thirty-one of them have not set one up yet — otherwise the
//   policy lands as a support queue rather than as a control.

using Microsoft.EntityFrameworkCore;
using DealerFOSS.Core;

namespace DealerFOSS.Identity;

internal sealed class SecurityPolicyService(IdentityDb db, IAuditSink audit) : ISecurityPolicy
{
    private readonly IdentityDb _db = db;
    private readonly IAuditSink _audit = audit;

    public async Task<IReadOnlyList<RoleSecondFactorPolicy>> ListSecondFactorPolicyAsync(
        CancellationToken cancellationToken)
    {
        // Users are counted distinctly: the same person may hold one role at
        // several rooftops, and they are still one person to chase.
        var rows = await _db.Roles
            .AsNoTracking()
            .OrderBy(role => role.Name)
            .Select(role => new
            {
                role.Id,
                role.Name,
                role.RequiresSecondFactor,
                Holders = _db.UserAssignments
                    .Where(a => a.RoleId == role.Id)
                    .Select(a => a.UserId)
                    .Distinct()
                    .Count(),
                Enrolled = _db.UserAssignments
                    .Where(a => a.RoleId == role.Id)
                    .Select(a => a.UserId)
                    .Distinct()
                    .Count(id => _db.Users.Any(u =>
                        u.Id == id && u.MfaConfirmedAt != null && u.MfaSecretProtected != null)),
            })
            .ToListAsync(cancellationToken);

        return rows
            .Select(row => new RoleSecondFactorPolicy(
                row.Id, row.Name, row.RequiresSecondFactor, row.Holders, row.Holders - row.Enrolled))
            .ToList();
    }

    public async Task<Result> RequireSecondFactorAsync(
        Guid roleId,
        bool required,
        Guid actingUserId,
        CancellationToken cancellationToken)
    {
        var role = await _db.Roles.SingleOrDefaultAsync(r => r.Id == roleId, cancellationToken);

        if (role is null)
        {
            return Result.Failure(AuthErrors.UnknownRole);
        }

        if (role.RequiresSecondFactor == required)
        {
            // Setting it to what it already is changes nothing and is not worth
            // an audit row that reads like something happened.
            return Result.Success();
        }

        role.RequireSecondFactor(required);
        await _db.SaveChangesAsync(cancellationToken);

        await _audit.RecordAsync(
            new AuditEntry(
                actingUserId,
                required ? "Security.SecondFactorRequired" : "Security.SecondFactorNoLongerRequired",
                AuditOutcome.Allowed,
                "Role",
                role.Id.ToString(),
                null,
                $"Second factor {(required ? "required of" : "no longer required of")} '{role.Name}'.",
                null,
                null),
            cancellationToken);

        return Result.Success();
    }
}
