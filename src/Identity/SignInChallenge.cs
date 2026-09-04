// Copyright (c) 2026 The DealerFOSS contributors.
// SPDX-License-Identifier: AGPL-3.0-or-later
//
// Overview: Purpose, File Design, and Engineering
//   SignInChallenge — the short window between "the password was right" and "the
//   code was right".
//
// Usage:
//   Created by Authenticator when a user with a second factor signs in;
//   consumed when they supply a code.
//
// Coding Instructions:
//   This is a credential in its own right, so it is stored hashed and lives
//   for minutes, not hours. It grants nothing on its own — presenting it
//   without a valid code does nothing — but a long-lived one would turn a
//   stolen password into a stolen account eventually.

namespace DealerFOSS.Identity;

internal sealed class SignInChallenge
{
    /// <summary>Long enough to find a phone, short enough to be useless if stolen.</summary>
    public static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(5);

    /// <summary>
    /// How many wrong codes are tolerated before the challenge dies. Six digits
    /// is a million possibilities; without a cap, an automated client would work
    /// through enough of them.
    /// </summary>
    public const int MaxAttempts = 5;

    public Guid Id { get; private set; }

    public Guid UserId { get; private set; }

    public string TokenHash { get; private set; } = string.Empty;

    public DateTimeOffset ExpiresAt { get; private set; }

    public DateTimeOffset? ConsumedAt { get; private set; }

    public int FailedAttempts { get; private set; }

    public string? DeviceSummary { get; private set; }

    private SignInChallenge()
    {
    }

    public SignInChallenge(Guid id, Guid userId, string tokenHash, DateTimeOffset now, string? deviceSummary)
    {
        Id = id;
        UserId = userId;
        TokenHash = tokenHash;
        ExpiresAt = now.Add(Lifetime);
        DeviceSummary = deviceSummary;
    }

    public bool IsUsableAt(DateTimeOffset now) =>
        ConsumedAt is null && FailedAttempts < MaxAttempts && now < ExpiresAt;

    public void RecordFailure() => FailedAttempts++;

    public void Consume(DateTimeOffset at) => ConsumedAt = at;
}
