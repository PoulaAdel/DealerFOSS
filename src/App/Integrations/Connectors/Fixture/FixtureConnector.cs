// FixtureConnector — a provider that misbehaves the way real ones do.
//
// Use:  the conformance tests run against this. It is shipped rather than kept
//       in the test project so a new connector author has a worked example of
//       what the awkward answers look like.
// Edit: every behaviour here was chosen because a real DMS does it. Do not
//       "tidy" one away — a fixture that only returns well-formed pages proves
//       nothing, and the conformance suite would pass against a connector that
//       cannot survive a Tuesday.

using DealerFOSS.Core;

namespace DealerFOSS.Integrations.Connectors.Fixture;

/// <summary>The awkward behaviours a connector must survive.</summary>
public enum FixtureBehaviour
{
    /// <summary>Serves exactly what was asked for and says so.</summary>
    Wellbehaved = 1,

    /// <summary>
    /// Returns records but never reports which period it covered. Common, and
    /// the reason a cursor may not advance on a request alone.
    /// </summary>
    SilentAboutCoverage = 2,

    /// <summary>
    /// Serves a later period than requested — the provider quietly clamped the
    /// start date. Advancing the cursor to the end of this leaves a hole.
    /// </summary>
    ClampsTheStart = 3,

    /// <summary>Serves less than asked for and says so honestly.</summary>
    ServesPartially = 4,
}

/// <summary>
/// A connector with no network behind it. Its manifest is a realistic one: a
/// chunked history window, a lookback limit, and typed settings.
/// </summary>
public sealed class FixtureConnector : IConnector
{
    private readonly FixtureBehaviour _behaviour;
    private readonly int _recordCount;

    public FixtureConnector(FixtureBehaviour behaviour = FixtureBehaviour.Wellbehaved, int recordCount = 2)
    {
        _behaviour = behaviour;
        _recordCount = recordCount;
    }

    /// <summary>The window a chunked history endpoint really imposes.</summary>
    public static FetchWindowPolicy HistoryWindow { get; } = new(
        AcceptsDates: true,
        MaximumChunk: TimeSpan.FromDays(183),
        MaximumLookback: TimeSpan.FromDays(365 * 5),
        SettlementDelay: TimeSpan.Zero);

    /// <summary>The window a delta endpoint that takes no dates imposes.</summary>
    public static FetchWindowPolicy DeltaWindow { get; } = new(
        AcceptsDates: false,
        MaximumChunk: null,
        MaximumLookback: null,
        SettlementDelay: TimeSpan.Zero);

    public ConnectorManifest Manifest { get; } = new(
        Provider: "Fixture",
        Version: "1.0",
        Certification: CertificationStatus.FixtureTested,
        Capabilities:
        [
            new ConnectorCapability("Deals", 1, SyncDirection.Read, HistoryWindow),
            new ConnectorCapability("Service", 1, SyncDirection.Read, DeltaWindow),
        ],
        Settings:
        [
            new ConnectorSetting("SubscriptionId", SettingKind.Text, Required: true, "The provider's subscription identifier."),
            new ConnectorSetting("DepartmentId", SettingKind.Text, Required: true, "The department the records belong to."),
            new ConnectorSetting("ApiSecret", SettingKind.Secret, Required: true, "Held encrypted; never shown again after saving."),
            new ConnectorSetting("SettlementDays", SettingKind.Number, Required: false, "How many days this dealership takes to post its paperwork."),
            new ConnectorSetting("UseSandbox", SettingKind.Toggle, Required: false, "Point at the provider's test environment."),
        ],
        Timing: ProviderTiming.Default,
        KnownLimitations:
        [
            "Serves fabricated records. Never certify anything against this.",
        ]);

    public Task<Result<FetchOutcome>> FetchAsync(
        ConnectorCapability capability,
        IReadOnlyDictionary<string, string?> settings,
        FetchSlice slice,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(capability);
        ArgumentNullException.ThrowIfNull(slice);
        cancellationToken.ThrowIfCancellationRequested();

        var records = Enumerable.Range(1, _recordCount)
            .Select(i => new ProviderRecord(
                $"FIX-{i:D4}",
                i.ToString(System.Globalization.CultureInfo.InvariantCulture),
                new Dictionary<string, string?>(StringComparer.Ordinal)
                {
                    ["dealNumber"] = $"FIX-{i:D4}",
                    ["frontGross"] = "1250.00",
                    ["contractDate"] = "2026-03-04",
                }))
            .ToList();

        var covered = slice.Range is not { } asked
            ? null
            : _behaviour switch
            {
                // Says nothing. The runtime must leave the cursor alone.
                FixtureBehaviour.SilentAboutCoverage => (DateRange?)null,

                // Quietly refuses the oldest part of the request.
                FixtureBehaviour.ClampsTheStart =>
                    new DateRange(asked.Start.AddDays(3), asked.End),

                // Honest about serving less.
                FixtureBehaviour.ServesPartially =>
                    new DateRange(asked.Start, asked.Start + ((asked.End - asked.Start) / 2)),

                _ => asked,
            };

        return Task.FromResult(Result.Success(new FetchOutcome(records, covered, [])));
    }
}
