// Clock — injectable "now", so time can be controlled in tests.
//
// Use:  inject IClock and read UtcNow. Do not call DateTimeOffset.UtcNow in
//       domain or workflow code.
// Edit: instants are UTC. A dealership-local date is derived from the rooftop
//       time zone at the point of display, not stored as local time.

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
