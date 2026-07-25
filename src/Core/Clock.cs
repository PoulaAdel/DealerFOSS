namespace OpenDealer360.Core;

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
