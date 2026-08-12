// IntegrationErrors — the stable codes an integration refusal is identified by.
//
// Use:  return Result.Failure<T>(IntegrationErrors.CoverageUnknown). Codes are
//       part of the API contract (doc 06 §6) and must not be reworded.
// Edit: adding a code is cheap; changing one breaks every caller matching on it.
//       Note which of these are *non-transient* — a retry loop that cannot tell
//       "the provider is down" from "the provider's record has no room" will
//       retry the second one forever.

using DealerFOSS.Core;

namespace DealerFOSS.Integrations;

/// <summary>
/// Refusals the integration edge produces. Transport failures are not here —
/// those are the connector's own business and are retried; these are outcomes a
/// person or an operator has to know about.
/// </summary>
public static class IntegrationErrors
{
    // --- Cursor safety (doc 05 §4) ---------------------------------------

    /// <summary>
    /// The provider did not report which range it served, so there is nothing
    /// safe to advance the cursor to. Not an error in the run — the records
    /// still arrived — but the cursor stays where it was and the same window is
    /// asked for again.
    /// </summary>
    public static readonly Error CoverageUnknown = Error.Conflict(
        "integration.coverage_unknown",
        "The provider did not report which period it covered, so the cursor cannot move.");

    /// <summary>
    /// The range the provider served starts after the cursor, so the period in
    /// between was never fetched. Advancing here would bury the hole.
    /// </summary>
    public static readonly Error CoverageGap = Error.Conflict(
        "integration.coverage_gap",
        "The provider covered a period starting after the cursor, leaving a gap.");

    // --- Data shape (ADR-021, doc 05 §2) ---------------------------------

    /// <summary>
    /// Two provider collections that are only aligned by position have
    /// different lengths, so nothing can be said about which value belongs to
    /// which. The record is quarantined rather than truncated to the shorter.
    /// </summary>
    public static readonly Error ColumnsMisaligned = Error.Validation(
        "integration.columns_misaligned",
        "Provider columns that are aligned by position have different lengths.");

    // --- Outbound (doc 05 §5) --------------------------------------------

    /// <summary>
    /// The provider's record has a fixed number of slots and the payload has
    /// more items than will fit. **Non-transient** — retrying cannot help, and
    /// writing the first N and reporting success is worse than refusing.
    /// </summary>
    public static readonly Error NoRoomInProviderRecord = Error.Conflict(
        "integration.no_room_in_provider_record",
        "The provider's record has fewer slots than this change needs.");

    // --- Configuration (doc 05 §3) ---------------------------------------

    /// <summary>
    /// A setting the manifest declares as required is missing or blank. This
    /// fails the dealership loudly on purpose: the alternative is a store that
    /// is quietly skipped every night for months.
    /// </summary>
    public static Error SettingMissing(string name) => Error.Validation(
        "integration.setting_missing",
        $"The connector setting '{name}' is required and was not supplied.");

    /// <summary>A setting's value is not of the declared kind.</summary>
    public static Error SettingInvalid(string name, string expected) => Error.Validation(
        "integration.setting_invalid",
        $"The connector setting '{name}' must be {expected}.");

    /// <summary>
    /// A setting was supplied that the manifest does not declare. Refused
    /// rather than ignored — a misspelled key that silently does nothing is the
    /// same failure as a missing one, discovered later.
    /// </summary>
    public static Error SettingUnknown(string name) => Error.Validation(
        "integration.setting_unknown",
        $"The connector does not have a setting called '{name}'.");

    // --- Runtime (doc 05 §4) ---------------------------------------------

    /// <summary>
    /// The connector offers a capability that nothing in the application knows
    /// how to apply. **Non-transient** — it is a deployment mistake, and
    /// fetching anyway would spend a provider's rate limit to throw the answer
    /// away.
    /// </summary>
    public static Error NoSinkRegistered(string contract, int version) => Error.Conflict(
        "integration.no_sink",
        $"Nothing is registered to apply '{contract}' v{version} records.");
}
