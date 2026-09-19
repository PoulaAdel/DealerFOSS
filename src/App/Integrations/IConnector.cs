// Copyright (c) 2026 The DealerFOSS contributors.
// SPDX-License-Identifier: AGPL-3.0-or-later
//
// Overview: Purpose, File Design, and Engineering
//   IConnector — the whole surface a provider adapter presents to the runtime.
//
// Usage:
//   Implement this in Connectors/<Provider>/. The runtime plans the window,
//   calls Fetch once per slice, and advances the cursor from what came back.
//
// Coding Instructions:
//   FetchOutcome.Covered is the load-bearing field. It is nullable because
//   "the provider did not say" is a real and common answer, and forcing a
//   value here would push every connector into guessing — which is the
//   failure this whole design exists to prevent.

using DealerFOSS.Core;

namespace DealerFOSS.Integrations;

/// <summary>One record, translated into contract vocabulary but not yet applied.</summary>
/// <param name="ExternalId">
/// The provider's identifier. This is what deduplication is keyed on, so a sink
/// can be called twice with the same record and produce one row.
/// </param>
/// <param name="ExternalVersion">
/// The provider's own version or sequence, preferred over its timestamps for
/// ordering. Null when the provider offers none.
/// </param>
/// <param name="Fields">
/// Values keyed by <strong>contract</strong> field names — see
/// <see cref="CustomerFields"/>. Not the provider's own names: translating is
/// the connector's job and doing it here, once, is what stops every sink having
/// to learn every provider's vocabulary.
/// <para>
/// Values are still raw text. A sink coerces them with <see cref="Coerce"/>, so
/// a value that will not fit becomes absent rather than a substitute (ADR-021).
/// A field the provider did not supply is simply missing from the dictionary,
/// which is different from present-and-empty and must stay so.
/// </para>
/// </param>
public sealed record ProviderRecord(
    string ExternalId,
    string? ExternalVersion,
    IReadOnlyDictionary<string, string?> Fields,
    RecordAction Action = RecordAction.Upsert);

/// <summary>
/// What the provider is saying about a record: that it exists, or that it is
/// gone.
/// </summary>
/// <remarks>
/// <para>
/// One enum on the record rather than a second method on
/// <see cref="IRecordSink"/>, because <b>deletes arrive interleaved with
/// upserts in the same delta feed and the order between them is the provider's
/// meaning</b>. "Created, then deleted" and "deleted, then created" are
/// different feeds describing different days, and splitting them into two
/// method calls throws that away — the sink would have no way to know which
/// came first.
/// </para>
/// <para>
/// Defaulted, so every existing connector and every test that builds a record
/// keeps saying what it already said.
/// </para>
/// </remarks>
public enum RecordAction
{
    /// <summary>The provider is serving this record as current.</summary>
    Upsert = 0,

    /// <summary>
    /// The provider no longer has this record. It is NOT an instruction to
    /// delete ours — see <see cref="Customers"/>'s sink and ADR-026. A
    /// dealership's own record of somebody it has done business with is not the
    /// provider's to remove.
    /// </summary>
    Delete = 1,
}

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
