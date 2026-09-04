// Copyright (c) 2026 The DealerFOSS contributors.
// SPDX-License-Identifier: AGPL-3.0-or-later
//
// Overview: Purpose, File Design, and Engineering
//   TenantRecord — one routing row in the control-plane catalog: how to reach a
//   dealer organization's database, and whether it is currently usable.
//
// Usage:
//   Read through TenantResolver, not directly.
//
// Coding Instructions:
//   Routing and lifecycle fields only. Dealership business data never goes
//   in the host catalog (ADR-003) — it belongs in the tenant database.

namespace DealerFOSS.Tenancy;

/// <summary>
/// A routing row in the control-plane catalog (`DealerFOSS_Host`). It records
/// how to reach a dealer organization's database and its lifecycle state — never
/// any dealership business data (doc 04 §2).
/// </summary>
public sealed class TenantRecord
{
    public Guid Id { get; set; }

    public required string Name { get; set; }

    /// <summary>Stable, URL-safe routing key (e.g. "north-auto-group").</summary>
    public required string Slug { get; set; }

    public TenantStatus Status { get; set; } = TenantStatus.Provisioning;

    /// <summary>Protected (envelope-encrypted) connection reference; never plaintext at rest.</summary>
    public required string ProtectedConnectionString { get; set; }

    /// <summary>Schema version the tenant database is currently migrated to.</summary>
    public int DatabaseVersion { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset? ModifiedAt { get; set; }
}

public enum TenantStatus
{
    Provisioning = 0,
    Active = 1,
    Suspended = 2,
    Retired = 3,
}
