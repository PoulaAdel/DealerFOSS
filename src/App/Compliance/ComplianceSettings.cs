// Copyright (c) 2026 The DealerFOSS contributors.
// SPDX-License-Identifier: AGPL-3.0-or-later
//
// Overview: Purpose, File Design, and Engineering
//   ComplianceSettings — the POSTURE: which jurisdiction packs this dealership
//   has chosen to use. One row per tenant database, in the tenant database.
//   Also the table configuration for it.
//
// Usage:
//   Read and changed through ICompliance. The resolver reads it on every
//   resolution, because a pack turned off this morning must stop charging now.
//
// Coding Instructions:
//   THIS IS THE ONE PIECE OF THE THREE-WAY SPLIT THAT IS PER TENANT (ADR-024).
//   The rate data is host-catalog reference data shared by every dealership; the
//   CHOICE to use it is the dealership's own, and it lives here with their other
//   business data. That division is the whole reason 23 states' quarterly files
//   are not copied into every tenant database.
//
//   DEFAULT IS OFF, AND THAT IS DELIBERATE. An installation that started
//   charging tax from whatever packs happened to ship would be the product
//   issuing a legal opinion about its operator's obligations, which ADR-024
//   refuses to do. Nothing resolves until a dealership says which pack is theirs.
//   Do not "improve" this into enabling every supported pack for the tenant's
//   country.
//
//   Stored as a newline-separated list rather than a child table. The whole
//   value is read and replaced as one, it is a handful of short ids, and nothing
//   ever queries across it — a table with its own migration, keys and cascade
//   would be ceremony around a list of strings. If a pack id ever needs its own
//   per-tenant attributes, that is the moment it becomes a table.

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using DealerFOSS.Core;
using DealerFOSS.Data;

namespace DealerFOSS.Compliance;

/// <summary>The schema this capability owns inside a tenant database.</summary>
internal static class ComplianceSchema
{
    public const string Name = "compliance";
}

/// <summary>
/// Which packs this dealer organization uses. One row per tenant database,
/// because one tenant is one dealer organization.
/// </summary>
public sealed class ComplianceSettings : AuditableEntity
{
    /// <summary>
    /// Fixed, for the same reason as <c>PartsSettings.SingletonId</c>: reading
    /// the setting is a primary-key lookup rather than a scan hoping for one row.
    /// </summary>
    public static readonly Guid SingletonId = new("c0119a7c-0000-4000-8000-000000000001");

    public Guid Id { get; private set; }

    /// <summary>
    /// Newline-separated pack ids. Empty means no pack is enabled, which is the
    /// default and means every tax line is entered by a person.
    /// </summary>
    private string _enabledPackIds = string.Empty;

    public IReadOnlyList<string> EnabledPackIds =>
        _enabledPackIds.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    private ComplianceSettings()
    {
    }

    public ComplianceSettings(IEnumerable<string> enabledPackIds)
    {
        Id = SingletonId;
        Enable(enabledPackIds);
    }

    public void Enable(IEnumerable<string> enabledPackIds)
    {
        ArgumentNullException.ThrowIfNull(enabledPackIds);

        _enabledPackIds = string.Join(
            '\n',
            enabledPackIds
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Select(id => id.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase));
    }
}

internal sealed class ComplianceSettingsConfiguration : IEntityTypeConfiguration<ComplianceSettings>
{
    public void Configure(EntityTypeBuilder<ComplianceSettings> builder)
    {
        builder.ToTable("ComplianceSettings", ComplianceSchema.Name);
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();

        // Mapped by field: the property is a computed view over the stored text,
        // so EF has to be pointed at the backing field or it would try to
        // persist a read-only list.
        builder.Property<string>("_enabledPackIds")
            .HasColumnName("EnabledPackIds")
            .HasMaxLength(1000)
            .IsRequired();

        builder.Ignore(x => x.EnabledPackIds);

        builder.ConfigureAudit();
    }
}
