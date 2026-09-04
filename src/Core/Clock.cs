// Copyright (c) 2026 The DealerFOSS contributors.
// SPDX-License-Identifier: AGPL-3.0-or-later
//
// Overview: Purpose, File Design, and Engineering
//   Injectable "now". An interface rather than a static call so that time is an
//   input to the system instead of an ambient fact about the machine it runs
//   on, which is what makes expiry, aging and period-close testable at all.
//
//   Instants are UTC throughout. A dealership-local date is DERIVED from the
//   rooftop's time zone at the point of display; it is never stored as local
//   time, because a stored local time cannot be interpreted later without also
//   knowing which zone and which rule set were in force when it was written.
//
// Usage:
//   Inject IClock, read UtcNow.
//   Do not call DateTimeOffset.UtcNow in domain or workflow code.
//
// Coding Instructions:
//   Keep this to the current instant. A "today in the rooftop's zone" helper
//   belongs where the rooftop is known, not here — Core knows nothing about
//   rooftops and must not learn.

namespace DealerFOSS.Core;

/// <summary>
/// Abstracts "now" so time is injectable and testable. Domain and application
/// code never reads <see cref="DateTimeOffset.UtcNow"/> directly. Instants are
/// UTC; dealership-local dates are derived from the tenant time zone (doc 04 §4).
/// </summary>
public interface IClock
{
    DateTimeOffset UtcNow { get; }
}

/// <summary>The real system clock. Registered as a singleton in the Host.</summary>
public sealed class SystemClock : IClock
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}
