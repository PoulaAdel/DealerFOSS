// ISafetyRecalls — what the public safety-recall record says about a car.
//
// Use:  a screen asks "should anybody look at this car before we sell it?".
// Edit: READ THE SUMMARY ON RecallReport BEFORE CHANGING ANY WORDING HERE. The
//       distinction between "campaigns that apply to this model" and "work this
//       car still needs" is the entire honesty of this feature, and it is easy
//       to erase by accident while tidying a property name.

using DealerFOSS.Core;

namespace DealerFOSS.Vehicles;

/// <summary>
/// Safety recall campaigns published by the road-safety regulator.
///
/// Separate from <see cref="IVehicles"/> because the answer does not come from
/// our database and can be unavailable: this depends on somebody else's service
/// being reachable, which nothing else in the product does. A failure here must
/// never stop a person recording or selling a car — it is one more thing to
/// check, not a gate.
/// </summary>
public interface ISafetyRecalls
{
    /// <summary>
    /// Campaigns for one vehicle already on our records, looked up by what the
    /// regulator indexes on — year, make and model.
    /// </summary>
    Task<Result<RecallReport>> ForVehicleAsync(Guid vehicleId, CancellationToken cancellationToken);
}

/// <summary>
/// What the regulator publishes for a year, make and model.
/// </summary>
/// <remarks>
/// <para>
/// <b>This is not a statement about one car.</b> The public recall data is
/// indexed by model, not by VIN, and it carries no record of whether a
/// particular vehicle has had the work performed — only the manufacturer holds
/// that. A car showing four campaigns here may have had all four done years ago
/// by a previous owner.
/// </para>
/// <para>
/// Which is why the property is <see cref="Campaigns"/> and not "OpenRecalls",
/// and why <see cref="AppliesToModelNotVehicle"/> exists and is always true: a
/// caller that renders this has to be handed the caveat along with the data,
/// rather than having to remember it. Telling a dealership a car is clear when
/// it is not is the failure that matters here, and it concerns somebody's brakes.
/// </para>
/// </remarks>
public sealed record RecallReport(
    Guid VehicleId,
    int ModelYear,
    string Make,
    string Model,
    IReadOnlyList<RecallCampaign> Campaigns)
{
    /// <summary>
    /// Always true, and carried in the response on purpose. Confirming whether
    /// this particular vehicle still needs any of these means asking the
    /// manufacturer, which we cannot yet do (doc 11 §3.1).
    /// </summary>
    public bool AppliesToModelNotVehicle => true;
}

/// <summary>One recall campaign, in the regulator's own terms.</summary>
public sealed record RecallCampaign(
    string CampaignNumber,
    string Manufacturer,
    string Component,
    string Summary,
    string Remedy,
    DateOnly? ReportedOn,
    /// <summary>The regulator's judgement that the car should not be driven.</summary>
    bool DoNotDrive,
    /// <summary>The regulator's judgement that the car should be parked outdoors.</summary>
    bool ParkOutside);

/// <summary>Stable error codes for recall lookups (doc 06 §6).</summary>
public static class RecallErrors
{
    /// <summary>
    /// The regulator's service did not answer, or did not answer in time.
    ///
    /// Deliberately distinct from "this car has no recalls" — a screen that
    /// renders silence as "nothing to worry about" would be inventing an
    /// all-clear out of a network timeout.
    /// </summary>
    public static Error Unavailable { get; } = Error.Unavailable(
        "recalls.unavailable",
        "The safety recall service could not be reached. This is not the same as the vehicle having no recalls — try again shortly.");

    /// <summary>
    /// The vehicle is recorded with an exceptional VIN and too little else to
    /// identify a model. A trailer with a frame-plate number cannot be looked up.
    /// </summary>
    public static Error NotIdentifiable { get; } = Error.Validation(
        "recalls.not_identifiable",
        "This vehicle does not carry enough identification to look up recalls.");
}
