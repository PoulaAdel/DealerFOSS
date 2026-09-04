// Copyright (c) 2026 The DealerFOSS contributors.
// SPDX-License-Identifier: AGPL-3.0-or-later
//
// Overview: Purpose, File Design, and Engineering
//   ConnectorCursor — how far a dealership has been read, and why it stopped there.
//
// Usage:
//   One row per connector, per dealership, per contract. The runtime loads
//   it, plans from it, and writes it back only when the provider accounted
//   for the window.
//
// Coding Instructions:
//   This is the row the whole FetchWindow design exists to protect, so the
//   two rules below are enforced here rather than trusted to callers.
//
//   Hold() exists because "the cursor did not move" is a state somebody has
//   to be able to see. Without it a held cursor and a healthy one look
//   identical in the table, and the store that has been re-reading the same
//   three days for a fortnight is invisible until a reconciliation finds the
//   hole.

using DealerFOSS.Core;

namespace DealerFOSS.Integrations;

/// <summary>
/// How far one dealership's feed has been read. Advances only from a range the
/// provider reported serving — never from a range that was merely requested.
/// </summary>
public sealed class ConnectorCursor : AuditableEntity
{
    public Guid Id { get; private set; }

    /// <summary>Provider name from the manifest.</summary>
    public string Connector { get; private set; } = string.Empty;

    public RooftopId RooftopId { get; private set; }

    /// <summary>The capability this cursor tracks; each is read independently.</summary>
    public string Contract { get; private set; } = string.Empty;

    public int Version { get; private set; }

    /// <summary>Everything before this instant has been read and applied.</summary>
    public DateTimeOffset Position { get; private set; }

    /// <summary>When the position last actually changed. Null until it first does.</summary>
    public DateTimeOffset? AdvancedAt { get; private set; }

    /// <summary>
    /// Set when the last run could not justify moving the cursor — the stable
    /// code from <see cref="IntegrationErrors"/>. Cleared on a successful
    /// advance, so a non-null value means "still stuck, right now".
    /// </summary>
    public string? HeldBecause { get; private set; }

    /// <summary>
    /// How many consecutive runs have failed to move it. The number is the
    /// signal: one is ordinary, six in a row is a dealership quietly falling
    /// behind and nobody being told.
    /// </summary>
    public int ConsecutiveHolds { get; private set; }

    private ConnectorCursor()
    {
    }

    public ConnectorCursor(
        Guid id,
        string connector,
        RooftopId rooftopId,
        string contract,
        int version,
        DateTimeOffset startingAt)
    {
        if (string.IsNullOrWhiteSpace(connector))
        {
            throw new ArgumentException("A connector name is required.", nameof(connector));
        }

        if (string.IsNullOrWhiteSpace(contract))
        {
            throw new ArgumentException("A contract is required.", nameof(contract));
        }

        Id = id;
        Connector = connector.Trim();
        RooftopId = rooftopId;
        Contract = contract.Trim();
        Version = version;
        Position = startingAt;
    }

    /// <summary>
    /// Move to the end of what the provider reported serving.
    /// </summary>
    /// <remarks>
    /// Takes the served range rather than a bare instant on purpose: the
    /// gap check in <see cref="Cursor.Advance"/> needs the range's <em>start</em>,
    /// and a signature taking only an instant would make the unsafe call the
    /// easy one to write.
    /// </remarks>
    public Result Advance(DateRange? covered, DateTimeOffset now)
    {
        var next = Cursor.Advance(Position, covered);
        if (next.IsFailure)
        {
            Hold(next.Error.Code);
            return Result.Failure(next.Error);
        }

        // A provider re-delivering an old page is not progress, but it is not a
        // hold either — the window was accounted for, there was simply nothing
        // beyond where we already are.
        if (next.Value > Position)
        {
            Position = next.Value;
            AdvancedAt = now;
        }

        HeldBecause = null;
        ConsecutiveHolds = 0;
        return Result.Success();
    }

    /// <summary>Record that this run could not justify moving, and why.</summary>
    private void Hold(string reasonCode)
    {
        HeldBecause = reasonCode;
        ConsecutiveHolds++;
    }
}
