// Copyright (c) 2026 The DealerFOSS contributors.
// SPDX-License-Identifier: AGPL-3.0-or-later
//
// Overview: Purpose, File Design, and Engineering
//   PollBudget — waiting on a job the provider has already accepted.
//
// Usage:
//   var budget = PollBudget.Start(timing, now); then budget.Next(now, hint)
//   each time the provider says "check back in N seconds".
//
// Coding Instructions:
//   Keep this measured in wall-clock time. A fixed number of attempts is
//   not a duration — the same five attempts are fifty seconds against a
//   provider that says "check back in 10" and fifty minutes against one
//   that says "600". Counting attempts produces a timeout the client
//   invented while the job was running perfectly well.

namespace DealerFOSS.Integrations;

/// <summary>
/// The two timing settings an async provider call needs. They are separate on
/// purpose: a flapping server and a slow job are different problems and must
/// not share one budget.
/// </summary>
/// <param name="PollDeadline">How long we will wait on a job the provider accepted.</param>
/// <param name="RetryBudget">How many times a failed <em>transport</em> attempt is retried.</param>
/// <param name="InitialBackoff">First retry delay; doubles from there.</param>
public sealed record ProviderTiming(TimeSpan PollDeadline, int RetryBudget, TimeSpan InitialBackoff)
{
    public static ProviderTiming Default { get; } = new(TimeSpan.FromMinutes(30), 5, TimeSpan.FromSeconds(2));
}

/// <summary>What to do next when polling an accepted job.</summary>
public enum PollDecision
{
    /// <summary>Wait the returned delay and ask again.</summary>
    WaitAndRetry = 1,

    /// <summary>
    /// The deadline passed. The job is still running at the provider — this is
    /// an operational state to report, not a failure to raise, because the
    /// right next action is to ask again later rather than to start over.
    /// </summary>
    StillRunningAtProvider = 2,
}

/// <summary>Tracks how long we have been waiting on one accepted job.</summary>
public sealed class PollBudget
{
    private readonly DateTimeOffset _deadline;

    private PollBudget(DateTimeOffset deadline) => _deadline = deadline;

    /// <summary>Attempts made so far. Reported, never used to decide.</summary>
    public int Attempts { get; private set; }

    public static PollBudget Start(ProviderTiming timing, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(timing);
        return new PollBudget(now + timing.PollDeadline);
    }

    /// <summary>
    /// Decide what to do next, given the provider's own "check back in" hint.
    /// </summary>
    /// <param name="now">The current time.</param>
    /// <param name="providerHint">
    /// The provider's suggested wait. Honoured as given — a provider asking for
    /// six hundred seconds knows something we do not — but never allowed to run
    /// past the deadline.
    /// </param>
    public (PollDecision Decision, TimeSpan Wait) Next(DateTimeOffset now, TimeSpan providerHint)
    {
        Attempts++;

        if (now >= _deadline)
        {
            return (PollDecision.StillRunningAtProvider, TimeSpan.Zero);
        }

        var wait = providerHint > TimeSpan.Zero ? providerHint : TimeSpan.FromSeconds(10);
        var remaining = _deadline - now;

        // Waiting past the deadline only delays the report; it never turns a
        // job that has not finished into one that has.
        return wait >= remaining
            ? (PollDecision.StillRunningAtProvider, TimeSpan.Zero)
            : (PollDecision.WaitAndRetry, wait);
    }
}
