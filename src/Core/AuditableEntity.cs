// Copyright (c) 2026 The DealerFOSS contributors.
// SPDX-License-Identifier: AGPL-3.0-or-later
//
// Overview: Purpose, File Design, and Engineering
//   The base for records carrying audit columns and an optimistic concurrency
//   stamp. Inheriting it is what opts a table into two behaviours the tenant
//   data context applies centrally in SaveChangesAsync: the columns are stamped
//   on save, and the stamp is checked so a second writer cannot silently
//   overwrite the first.
//
//   Central rather than per-entity for the same reason IAppendOnly is an
//   interface: the failure mode of forgetting is invisible. A row with no
//   CreatedBy looks like a row, not like a bug.
//
// Usage:
//   Inherit from it on any business record.
//   Never set CreatedAt / ModifiedAt / CreatedBy / ModifiedBy by hand.
//
// Coding Instructions:
//   Change this rarely and deliberately. A field added here changes EVERY table
//   that inherits it, so it needs a migration for each — across four contexts.
//   Architecture tests also assert that anything inheriting this has no EF or
//   ASP.NET dependency, wherever the file happens to sit.

namespace DealerFOSS.Core;

/// <summary>
/// Base for business records that carry audit columns and an optimistic
/// concurrency stamp (doc 04 §4). The stamp and audit fields are set centrally
/// by the owning module's data context on save — not by hand at each call site
/// (doc 08 §5). This base is deliberately EF-free; mapping is configured in each
/// module's Data layer.
/// </summary>
public abstract class AuditableEntity
{
    public DateTimeOffset CreatedAt { get; set; }

    public string CreatedBy { get; set; } = "system";

    public DateTimeOffset? ModifiedAt { get; set; }

    public string? ModifiedBy { get; set; }

    /// <summary>Optimistic-concurrency token; refreshed on every save.</summary>
    public Guid ConcurrencyStamp { get; set; } = Guid.NewGuid();
}
