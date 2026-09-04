// Copyright (c) 2026 The DealerFOSS contributors.
// SPDX-License-Identifier: AGPL-3.0-or-later
//
// Overview: Purpose, File Design, and Engineering
//   AdminSession — one signed-in administrator, held in the host catalog so it can
//   be revoked immediately.
//
// Usage:
//   Created by GlobalAdministrationService; validated on every /api/v1/admin
//   request.
//
// Coding Instructions:
//   The timeouts are deliberately tighter than a tenant user's. A dealership
//   session is a workday; a control-plane session is a task. Both secrets are
//   stored hashed, and both die together when the session is revoked — the
//   same rules Session follows, kept separate rather than shared because the
//   two must never be interchangeable.

namespace DealerFOSS.Identity;

internal sealed class AdminSession
{
    /// <summary>Tighter than a tenant session: the control plane is not a workday.</summary>
    public static TimeSpan IdleTimeout { get; } = TimeSpan.FromMinutes(15);

    /// <summary>The longest an administrator session may live regardless of activity.</summary>
    public static TimeSpan AbsoluteTimeout { get; } = TimeSpan.FromHours(4);

    public Guid Id { get; private set; }

    public Guid AdministratorId { get; private set; }

    /// <summary>SHA-256 of the token handed to the browser. The token itself is never stored.</summary>
    public string TokenHash { get; private set; } = string.Empty;

    /// <summary>SHA-256 of this session's anti-forgery token, bound to this session alone.</summary>
    public string AntiForgeryHash { get; private set; } = string.Empty;

    public DateTimeOffset IssuedAt { get; private set; }

    public DateTimeOffset LastSeenAt { get; private set; }

    public DateTimeOffset AbsoluteExpiresAt { get; private set; }

    public DateTimeOffset? RevokedAt { get; private set; }

    public string? DeviceSummary { get; private set; }

    private AdminSession()
    {
    }

    public AdminSession(
        Guid id,
        Guid administratorId,
        string tokenHash,
        string antiForgeryHash,
        DateTimeOffset now,
        string? deviceSummary)
    {
        if (string.IsNullOrWhiteSpace(tokenHash))
        {
            throw new ArgumentException("A session needs a token hash.", nameof(tokenHash));
        }

        if (string.IsNullOrWhiteSpace(antiForgeryHash))
        {
            throw new ArgumentException(
                "A session needs an anti-forgery hash, or its writes cannot be protected.",
                nameof(antiForgeryHash));
        }

        Id = id;
        AdministratorId = administratorId;
        TokenHash = tokenHash;
        AntiForgeryHash = antiForgeryHash;
        IssuedAt = now;
        LastSeenAt = now;
        AbsoluteExpiresAt = now.Add(AbsoluteTimeout);
        DeviceSummary = deviceSummary;
    }

    public bool IsRevoked => RevokedAt is not null;

    public bool IsActiveAt(DateTimeOffset now) =>
        !IsRevoked
        && now < AbsoluteExpiresAt
        && now < LastSeenAt.Add(IdleTimeout);

    /// <summary>Slides the idle window forward. Never moves the absolute ceiling.</summary>
    public void Touch(DateTimeOffset now)
    {
        if (!IsActiveAt(now))
        {
            throw new InvalidOperationException("An inactive session cannot be renewed.");
        }

        LastSeenAt = now;
    }

    public void Revoke(DateTimeOffset now) => RevokedAt ??= now;
}
