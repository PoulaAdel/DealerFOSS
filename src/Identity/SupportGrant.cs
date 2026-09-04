// Copyright (c) 2026 The DealerFOSS contributors.
// SPDX-License-Identifier: AGPL-3.0-or-later
//
// Overview: Purpose, File Design, and Engineering
//   SupportGrant — one recorded, time-limited entry by an administrator into one
//   dealership's data.
//
// Usage:
//   Created by GlobalAdministrationService.GrantSupportAccessAsync.
//
// Coding Instructions:
//   This record is the evidence, not the mechanism. The access itself is a
//   real tenant session with its own expiry, so ending the grant and ending
//   the session are the same act — do not let them drift apart, or the log
//   would say "closed" while the session kept working.
//
//   A reason is required and cannot be blank. An entry nobody had to justify
//   is not deliberate access; it is a back door with a timestamp.

namespace DealerFOSS.Identity;

internal sealed class SupportGrant
{
    /// <summary>The longest a single grant may run. Longer means asking again.</summary>
    public static TimeSpan MaximumDuration { get; } = TimeSpan.FromHours(1);

    /// <summary>What a caller gets if it does not ask for a specific window.</summary>
    public static TimeSpan DefaultDuration { get; } = TimeSpan.FromMinutes(30);

    public Guid Id { get; private set; }

    public Guid AdministratorId { get; private set; }

    /// <summary>The dealer organization entered, by routing key.</summary>
    public string TenantSlug { get; private set; } = string.Empty;

    /// <summary>Why. Recorded in the dealership's own audit trail as well as here.</summary>
    public string Reason { get; private set; } = string.Empty;

    /// <summary>The tenant session minted for this grant, so ending one ends the other.</summary>
    public Guid TenantSessionId { get; private set; }

    /// <summary>The support principal in that tenant the session belongs to.</summary>
    public Guid TenantUserId { get; private set; }

    public DateTimeOffset GrantedAt { get; private set; }

    public DateTimeOffset ExpiresAt { get; private set; }

    /// <summary>Set when it is ended early. Expiry needs no row change.</summary>
    public DateTimeOffset? EndedAt { get; private set; }

    private SupportGrant()
    {
    }

    public SupportGrant(
        Guid id,
        Guid administratorId,
        string tenantSlug,
        string reason,
        Guid tenantSessionId,
        Guid tenantUserId,
        DateTimeOffset now,
        TimeSpan duration)
    {
        if (string.IsNullOrWhiteSpace(tenantSlug))
        {
            throw new ArgumentException("A grant names the dealership entered.", nameof(tenantSlug));
        }

        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new ArgumentException(
                "A grant needs a reason. Access nobody had to justify is not deliberate.",
                nameof(reason));
        }

        Id = id;
        AdministratorId = administratorId;
        TenantSlug = tenantSlug;
        Reason = reason.Trim();
        TenantSessionId = tenantSessionId;
        TenantUserId = tenantUserId;
        GrantedAt = now;
        ExpiresAt = now.Add(Clamp(duration));
    }

    public bool IsActiveAt(DateTimeOffset now) => EndedAt is null && now < ExpiresAt;

    /// <summary>Ends it now. Ending twice is harmless.</summary>
    public void End(DateTimeOffset now) => EndedAt ??= now;

    /// <summary>
    /// A request for a longer window is shortened rather than refused, and one for
    /// a nonsensical window gets the default. The ceiling is not negotiable.
    /// </summary>
    public static TimeSpan Clamp(TimeSpan requested) =>
        requested <= TimeSpan.Zero ? DefaultDuration
        : requested > MaximumDuration ? MaximumDuration
        : requested;
}
