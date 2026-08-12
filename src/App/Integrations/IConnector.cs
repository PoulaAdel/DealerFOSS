// IConnector — the whole surface a provider adapter presents to the runtime.
//
// Use:  implement this in Connectors/<Provider>/. The runtime plans the window,
//       calls Fetch once per slice, and advances the cursor from what came back.
// Edit: FetchOutcome.Covered is the load-bearing field. It is nullable because
//       "the provider did not say" is a real and common answer, and forcing a
//       value here would push every connector into guessing — which is the
//       failure this whole design exists to prevent.

using DealerFOSS.Core;

namespace DealerFOSS.Integrations;

/// <summary>One record as the provider sent it, before mapping.</summary>
/// <param name="ExternalId">The provider's identifier, used to deduplicate.</param>
/// <param name="ExternalVersion">
/// The provider's own version or sequence, preferred over its timestamps for
/// ordering. Null when the provider offers none.
/// </param>
/// <param name="Fields">The raw values, keyed by the provider's own field names.</param>
public sealed record ProviderRecord(
    string ExternalId,
    string? ExternalVersion,
    IReadOnlyDictionary<string, string?> Fields);

/// <summary>What one fetch returned.</summary>
/// <param name="Records">The records served, in the order the provider gave them.</param>
/// <param name="Covered">
/// The period the provider reported actually serving. **Null when it did not
/// say** — in which case the cursor does not move, and the same window is asked
/// for again next run. Never fill this in from the range that was requested.
/// </param>
/// <param name="Warnings">Values that did not survive the crossing intact.</param>
public sealed record FetchOutcome(
    IReadOnlyList<ProviderRecord> Records,
    DateRange? Covered,
    IReadOnlyList<MappingWarning> Warnings)
{
    public static FetchOutcome Empty { get; } = new([], null, []);
}

/// <summary>
/// A provider adapter. One folder per connector (ADR-011); vendor DTOs and
/// protocol details stay behind this interface.
/// </summary>
public interface IConnector
{
    /// <summary>What this connector is and needs. Constant for a given build.</summary>
    ConnectorManifest Manifest { get; }

    /// <summary>
    /// Fetch one planned slice for one dealership.
    /// </summary>
    /// <param name="capability">The contract being read, from the manifest.</param>
    /// <param name="settings">
    /// This dealership's settings, already validated against the manifest.
    /// </param>
    /// <param name="slice">
    /// The slice to request. <see cref="FetchSlice.Range"/> is null for
    /// endpoints that take no dates and decide "recent" themselves.
    /// </param>
    Task<Result<FetchOutcome>> FetchAsync(
        ConnectorCapability capability,
        IReadOnlyDictionary<string, string?> settings,
        FetchSlice slice,
        CancellationToken cancellationToken);
}
