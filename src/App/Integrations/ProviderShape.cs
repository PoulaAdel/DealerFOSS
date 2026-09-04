// Copyright (c) 2026 The DealerFOSS contributors.
// SPDX-License-Identifier: AGPL-3.0-or-later
//
// Overview: Purpose, File Design, and Engineering
//   ProviderShape — the two shapes DMS payloads take that break naive mapping.
//
// Usage:
//   ColumnSet.Align() before reading parallel arrays; Slots.Fit() before
//   writing into a record with a fixed number of positions.
//
// Coding Instructions:
//   Both of these exist because the obvious code is wrong in a way that
//   produces no error. Keep them returning Result rather than throwing —
//   one quarantines a record and the other is a refusal a user reads.

using DealerFOSS.Core;

namespace DealerFOSS.Integrations;

/// <summary>
/// Provider collections whose correspondence is implied by position alone —
/// amounts in one array, pay types in another; operation codes in one,
/// descriptions and hours in others.
/// </summary>
/// <remarks>
/// Position is not a join key. Where both sides carry a real key, join on it and
/// do not use this type at all. This exists for the common case where no key is
/// offered, and its whole job is to make the positional read <em>checked</em>:
/// unequal lengths quarantine the record instead of silently truncating to the
/// shorter array, which is how a labour line ends up wearing another line's
/// hours.
/// </remarks>
public static class ColumnSet
{
    /// <summary>
    /// Verify that every named column has the same length, and return the row
    /// count. Fails when they disagree, naming the columns and their lengths so
    /// the quarantine message says something useful.
    /// </summary>
    public static Result<int> Align(IReadOnlyDictionary<string, int> columnLengths)
    {
        ArgumentNullException.ThrowIfNull(columnLengths);

        if (columnLengths.Count == 0)
        {
            return Result.Success(0);
        }

        var lengths = columnLengths.Values.Distinct().ToList();
        if (lengths.Count == 1)
        {
            return Result.Success(lengths[0]);
        }

        var detail = string.Join(", ", columnLengths.OrderBy(c => c.Key, StringComparer.Ordinal)
            .Select(c => $"{c.Key}={c.Value}"));

        return Result.Failure<int>(Error.Validation(
            IntegrationErrors.ColumnsMisaligned.Code,
            $"{IntegrationErrors.ColumnsMisaligned.Message} ({detail})"));
    }
}

/// <summary>
/// What would not fit when a payload is written into a record with a fixed
/// number of positions.
/// </summary>
public sealed record SlotOverflow(string What, int Available, int Needed)
{
    public int Excess => Needed - Available;
}

/// <summary>
/// DMS records frequently have fixed arity rather than collections — five fee
/// positions, three insurance positions, a numbered set of options. Writing
/// six things into five is a refusal, not a rounding error.
/// </summary>
public static class Slots
{
    /// <summary>
    /// Check that <paramref name="needed"/> items fit in <paramref name="available"/>
    /// positions.
    /// </summary>
    /// <remarks>
    /// The failure is deliberately a Conflict rather than a transient error: no
    /// number of retries creates a sixth slot, and writing the first five and
    /// reporting success hides a missing fee on a signed deal.
    /// </remarks>
    public static Result<SlotOverflow> Fit(string what, int available, int needed)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(what);
        ArgumentOutOfRangeException.ThrowIfNegative(available);
        ArgumentOutOfRangeException.ThrowIfNegative(needed);

        var outcome = new SlotOverflow(what, available, needed);

        return needed <= available
            ? Result.Success(outcome)
            : Result.Failure<SlotOverflow>(Error.Conflict(
                IntegrationErrors.NoRoomInProviderRecord.Code,
                $"The provider holds {available} {what} and this change needs {needed}. " +
                $"{outcome.Excess} would be lost, so nothing was sent."));
    }
}
