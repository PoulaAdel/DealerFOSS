// Copyright (c) 2026 The DealerFOSS contributors.
// SPDX-License-Identifier: AGPL-3.0-or-later
//
// Overview: Purpose, File Design, and Engineering
//   Administrator — whoever runs the deployment. Deliberately not a User.
//
// Usage:
//   Created by ControlPlaneSeeder today; an administration workflow later.
//
// Coding Instructions:
//   This type must never gain a link to a tenant, a rooftop, or a role from
//   the permission catalogue. The whole point is that operating the
//   installation and reading a dealership's records are different jobs held
//   by different records in different databases. An administrator reaches
//   business data only by going through the support-access flow, which mints
//   a separate, time-limited tenant session that the dealership can see.
//
//   A second factor is mandatory, not optional: an account that can suspend
//   a dealership or step into one is worth more to an attacker than any
//   single user account.

namespace DealerFOSS.Identity;

/// <summary>
/// A control-plane identity (doc 06 §2). Lives in the host catalog, not in any
/// tenant's database, and holds no permissions from the tenant catalogue.
/// </summary>
internal sealed class Administrator
{
    public Guid Id { get; private set; }

    public string Email { get; private set; } = string.Empty;

    public string DisplayName { get; private set; } = string.Empty;

    public bool IsActive { get; private set; } = true;

    /// <summary>
    /// Hashed credential. Null means this account cannot sign in at all — the
    /// same rule tenant users follow, for the same reason.
    /// </summary>
    public string? PasswordHash { get; private set; }

    /// <summary>The TOTP shared secret, encrypted with <c>ISecretProtector</c>.</summary>
    public string? MfaSecretProtected { get; private set; }

    /// <summary>Null while enrolment is half-finished, so a mis-scanned QR strands nobody.</summary>
    public DateTimeOffset? MfaConfirmedAt { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public bool MfaEnabled => MfaConfirmedAt is not null && MfaSecretProtected is not null;

    public bool CanSignIn => IsActive && PasswordHash is not null;

    private Administrator()
    {
    }

    public Administrator(Guid id, string email, string displayName, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            throw new ArgumentException("Email is required.", nameof(email));
        }

        if (string.IsNullOrWhiteSpace(displayName))
        {
            throw new ArgumentException("Display name is required.", nameof(displayName));
        }

        Id = id;
        Email = email.Trim().ToLowerInvariant();
        DisplayName = displayName;
        CreatedAt = now;
    }

    public void SetPasswordHash(string passwordHash)
    {
        if (string.IsNullOrWhiteSpace(passwordHash))
        {
            throw new ArgumentException("A password hash is required.", nameof(passwordHash));
        }

        PasswordHash = passwordHash;
    }

    public void Deactivate() => IsActive = false;

    /// <summary>Stores a generated secret without switching the second factor on.</summary>
    public void BeginMfaEnrolment(string protectedSecret)
    {
        if (string.IsNullOrWhiteSpace(protectedSecret))
        {
            throw new ArgumentException("An enrolment needs a secret.", nameof(protectedSecret));
        }

        MfaSecretProtected = protectedSecret;
        MfaConfirmedAt = null;
    }

    /// <summary>Switches it on, once a working code has been produced.</summary>
    public void ConfirmMfa(DateTimeOffset at)
    {
        if (MfaSecretProtected is null)
        {
            throw new InvalidOperationException("There is no enrolment to confirm.");
        }

        MfaConfirmedAt = at;
    }
}
