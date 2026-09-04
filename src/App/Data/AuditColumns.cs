// Copyright (c) 2026 The DealerFOSS contributors.
// SPDX-License-Identifier: AGPL-3.0-or-later
//
// Overview: Purpose, File Design, and Engineering
//   AuditColumns — the audit and concurrency columns every business record carries.
//
// Usage:
//   Builder.ConfigureAudit() at the end of an entity configuration.
//
// Coding Instructions:
//   This is shared by every feature's XTables.cs, so a change here changes
//   every table that inherits AuditableEntity and needs a migration.

using Microsoft.EntityFrameworkCore.Metadata.Builders;
using DealerFOSS.Core;

namespace DealerFOSS.Data;

/// <summary>
/// Maps the columns declared by <see cref="AuditableEntity"/>. Written once
/// rather than repeated in every configuration, so the concurrency token cannot
/// be forgotten on a new table (doc 08 §5).
/// </summary>
public static class AuditColumns
{
    public static EntityTypeBuilder<TEntity> ConfigureAudit<TEntity>(this EntityTypeBuilder<TEntity> builder)
        where TEntity : AuditableEntity
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.Property(x => x.CreatedBy).HasMaxLength(120).IsRequired();
        builder.Property(x => x.ModifiedBy).HasMaxLength(120);
        builder.Property(x => x.ConcurrencyStamp).IsConcurrencyToken();

        return builder;
    }
}
