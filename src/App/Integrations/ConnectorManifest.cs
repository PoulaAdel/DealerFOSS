// Copyright (c) 2026 The DealerFOSS contributors.
// SPDX-License-Identifier: AGPL-3.0-or-later
//
// Overview: Purpose, File Design, and Engineering
//   ConnectorManifest — what a connector claims, and what it needs to be told.
//
// Usage:
//   Every connector exposes one Manifest. The UI shows it, and
//   ValidateSettings() runs when a dealership's configuration is saved.
//
// Coding Instructions:
//   Settings are declared one field at a time. Never add a "Delimited" kind
//   or a setting whose value is parsed into several — that is how
//   "sub123;dept-a;dept-b;test" happens, where appending a word silently
//   moves a dealership to the sandbox and the parser for it becomes the
//   least-tested code in the connector.

using DealerFOSS.Core;

namespace DealerFOSS.Integrations;

/// <summary>How far a connector has been proven (doc 05 §3).</summary>
public enum CertificationStatus
{
    /// <summary>Automated tests only. Not a production promise.</summary>
    FixtureTested = 1,

    /// <summary>Passed the vendor sandbox and the failure/replay tests.</summary>
    SandboxCertified = 2,

    /// <summary>Completed a dealer-approved shadow comparison and reconciliation.</summary>
    ProductionCertified = 3,

    /// <summary>Community or experimental, with limitations stated.</summary>
    Experimental = 4,
}

/// <summary>Which way data moves for a capability.</summary>
[Flags]
public enum SyncDirection
{
    None = 0,
    Read = 1,
    Write = 2,
}

/// <summary>The type of a declared setting. Deliberately few.</summary>
public enum SettingKind
{
    Text = 1,
    Number = 2,

    /// <summary>Held encrypted, never returned to the UI, never written to a capture.</summary>
    Secret = 3,

    Toggle = 4,
}

/// <summary>
/// One per-dealership setting the connector needs, declared so it can be
/// validated when it is saved rather than discovered at three in the morning.
/// </summary>
public sealed record ConnectorSetting(string Name, SettingKind Kind, bool Required, string Description);

/// <summary>One capability this connector supports, and how.</summary>
public sealed record ConnectorCapability(
    string Contract,
    int Version,
    SyncDirection Direction,
    FetchWindowPolicy Window);

/// <summary>
/// What a connector is, what it can do, and what it must be told. The UI
/// displays these facts; nothing else may claim them on a connector's behalf.
/// </summary>
public sealed record ConnectorManifest(
    string Provider,
    string Version,
    CertificationStatus Certification,
    IReadOnlyList<ConnectorCapability> Capabilities,
    IReadOnlyList<ConnectorSetting> Settings,
    ProviderTiming Timing,
    IReadOnlyList<string> KnownLimitations)
{
    /// <summary>Find a capability, or null when this connector does not offer it.</summary>
    public ConnectorCapability? Capability(string contract, int version) =>
        Capabilities.FirstOrDefault(c =>
            string.Equals(c.Contract, contract, StringComparison.OrdinalIgnoreCase) && c.Version == version);

    /// <summary>
    /// Check a dealership's configuration against what this connector declares.
    /// </summary>
    /// <remarks>
    /// Two rules, and the second matters as much as the first:
    /// <list type="bullet">
    /// <item>a required setting that is missing or blank fails — <em>loudly</em>,
    /// for that dealership, so it is marked broken rather than quietly skipped
    /// every night;</item>
    /// <item>a setting the manifest does not declare fails too. A misspelled key
    /// that silently does nothing is the same failure as a missing one, found
    /// much later.</item>
    /// </list>
    /// </remarks>
    public Result ValidateSettings(IReadOnlyDictionary<string, string?> supplied)
    {
        ArgumentNullException.ThrowIfNull(supplied);

        foreach (var name in supplied.Keys)
        {
            if (!Settings.Any(s => string.Equals(s.Name, name, StringComparison.Ordinal)))
            {
                return Result.Failure(IntegrationErrors.SettingUnknown(name));
            }
        }

        foreach (var setting in Settings)
        {
            supplied.TryGetValue(setting.Name, out var value);
            var blank = string.IsNullOrWhiteSpace(value);

            if (blank)
            {
                if (setting.Required)
                {
                    return Result.Failure(IntegrationErrors.SettingMissing(setting.Name));
                }

                continue;
            }

            var wellFormed = setting.Kind switch
            {
                SettingKind.Number => long.TryParse(value, out _),
                SettingKind.Toggle => bool.TryParse(value, out _),
                _ => true,
            };

            if (!wellFormed)
            {
                var expected = setting.Kind == SettingKind.Number ? "a whole number" : "true or false";
                return Result.Failure(IntegrationErrors.SettingInvalid(setting.Name, expected));
            }
        }

        return Result.Success();
    }
}
