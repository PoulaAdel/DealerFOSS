// IRecordSink — how an arriving record reaches the capability that owns it.
//
// Use:  implemented by a capability (Deals, Customers, …) and registered in DI.
//       The runtime finds the sink for a contract and hands it the records.
// Edit: this interface is the reason Integrations can persist and apply anything
//       at all without seeing a single capability type. FeatureBoundaryTests
//       forbids the direct reference, and the correct response to that test
//       failing is another sink — never an exception to the rule. A connector
//       able to write a Deal row directly would be a route around every rule
//       Deals enforces, arriving from outside the building.
//
//       Two obligations on an implementer, both load-bearing:
//
//       1. APPLYING MUST BE IDEMPOTENT, keyed on ExternalId. The cursor stays
//          put whenever a provider will not account for the window, which means
//          the same records arrive again tomorrow — by design. A sink that
//          inserts blindly turns that safety into duplicate deals.
//       2. DO NOT SAVE. The runtime owns the transaction so that applying and
//          advancing the cursor commit together; a sink that saves on its own
//          can leave records applied under a cursor that then rolls back.

using DealerFOSS.Core;

namespace DealerFOSS.Integrations;

/// <summary>One record the sink refused, and why.</summary>
/// <param name="Record">The record as it arrived, kept for the quarantine row.</param>
/// <param name="Reason">A stable error explaining the refusal.</param>
public sealed record RejectedRecord(ProviderRecord Record, Error Reason);

/// <summary>What a sink did with a batch.</summary>
/// <param name="Applied">How many records were written or updated.</param>
/// <param name="Rejected">The ones that could not be, each with its reason.</param>
/// <param name="Warnings">
/// Values that were stored but did not survive intact (ADR-021). These do not
/// stop a record; they are counted on the run so a feed quietly degrading is
/// visible before somebody notices the figures are wrong.
/// </param>
public sealed record ApplyOutcome(
    int Applied,
    IReadOnlyList<RejectedRecord> Rejected,
    IReadOnlyList<MappingWarning> Warnings)
{
    public static ApplyOutcome Nothing { get; } = new(0, [], []);
}

/// <summary>
/// The capability-side half of an integration. One per contract and version.
/// </summary>
public interface IRecordSink
{
    /// <summary>The capability this sink accepts, matching a manifest contract.</summary>
    string Contract { get; }

    int Version { get; }

    /// <summary>
    /// Apply a batch for one dealership. Must be idempotent on
    /// <see cref="ProviderRecord.ExternalId"/>, and must not save — see the
    /// header for why both matter.
    /// </summary>
    Task<Result<ApplyOutcome>> ApplyAsync(
        RooftopId rooftopId,
        IReadOnlyList<ProviderRecord> records,
        CancellationToken cancellationToken);
}
