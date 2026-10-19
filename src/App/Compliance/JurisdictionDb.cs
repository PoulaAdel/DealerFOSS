// Copyright (c) 2026 The DealerFOSS contributors.
// SPDX-License-Identifier: AGPL-3.0-or-later
//
// Overview: Purpose, File Design, and Engineering
//   JurisdictionDb — where jurisdiction reference data is stored: the host
//   catalog database, under the "compliance" schema and no other.
//
// Usage:
//   Injected into ComplianceService and the development seeder. No feature code
//   touches it; Deals reaches the rates through ITaxResolution.
//
// Coding Instructions:
//   THIS IS THE HOST CATALOG, NOT A TENANT DATABASE, and that is a deliberate
//   reading of ADR-003 rather than an exception to it. ADR-003 keeps a
//   DEALERSHIP'S BUSINESS DATA out of the host catalog; a published tax rate is
//   not a dealership's business data, it is the same number for every dealership
//   in the state. Copying 23 states' quarterly files into every tenant database
//   would be the ZIP-code mistake one level up: duplicated reference data with
//   one chance per tenant to be a version behind. Only the ENABLEMENT is per
//   tenant, and that lives in ComplianceSettings next door.
//
//   The same separation as ControlPlaneDb, and for the same reason: a separate
//   context with its own schema means there is no code path by which a rate
//   table and a dealership's business tables are open in one unit of work. That
//   keeps HostDb's "routing only" rule literally true rather than approximately
//   true.
//
//   Migrations live in this capability's own flat Migrations/ folder, which is
//   what the analyzer exclusion [**/Migrations/*.cs] matches. They are generated
//   artifacts — never hand-edited.

using Microsoft.EntityFrameworkCore;

namespace DealerFOSS.Compliance;

/// <summary>
/// Jurisdiction reference data, in the host catalog (doc 04 §2), under its own
/// schema. One per deployment, shared by every tenant that enables a pack.
/// </summary>
internal sealed class JurisdictionDb(DbContextOptions<JurisdictionDb> options)
    : DbContext(options)
{
    public const string Schema = "compliance";

    public DbSet<JurisdictionPack> Packs => Set<JurisdictionPack>();

    public DbSet<JurisdictionRate> Rates => Set<JurisdictionRate>();

    public DbSet<JurisdictionRule> Rules => Set<JurisdictionRule>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);

        modelBuilder.Entity<JurisdictionPack>(pack =>
        {
            pack.ToTable("JurisdictionPacks");
            pack.HasKey(x => x.Id);
            pack.Property(x => x.Id).ValueGeneratedNever();
            pack.Property(x => x.PackId).HasMaxLength(64).IsRequired();
            pack.Property(x => x.Country).HasMaxLength(2).IsRequired();
            pack.Property(x => x.Scheme).HasConversion<string>().HasMaxLength(20);
            pack.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
            pack.Property(x => x.SourceUri).HasMaxLength(500).IsRequired();
            pack.Property(x => x.ContentHash).HasMaxLength(64).IsRequired();
            pack.Property(x => x.ReviewedBy).HasMaxLength(320);

            // One generation per pack per version, enforced by the database rather
            // than by the loader checking first. Two concurrent loads of the same
            // version are a duplicate-key error, which is recoverable; two
            // generations claiming to be version 3 is a rate table nobody can
            // reason about, which is not.
            pack.HasIndex(x => new { x.PackId, x.Version }).IsUnique();

            pack.HasMany(x => x.Rates)
                .WithOne()
                .HasForeignKey(r => r.PackRowId)
                .OnDelete(DeleteBehavior.Cascade);

            pack.HasMany(x => x.Rules)
                .WithOne()
                .HasForeignKey(r => r.PackRowId)
                .OnDelete(DeleteBehavior.Cascade);

            // Both collections are always wanted: resolving a rate needs the
            // flags, and the review gate needs to count the flags. Two queries
            // per resolution would be two round trips for data that is never
            // useful apart.
            pack.Navigation(x => x.Rates).AutoInclude();
            pack.Navigation(x => x.Rules).AutoInclude();
        });

        modelBuilder.Entity<JurisdictionRate>(rate =>
        {
            rate.ToTable("JurisdictionRates");
            rate.HasKey(x => x.Id);
            rate.Property(x => x.Id).ValueGeneratedNever();
            rate.Property(x => x.JurisdictionCode).HasMaxLength(32).IsRequired();
            rate.Property(x => x.Description).HasMaxLength(200).IsRequired();
            rate.Property(x => x.Kind).HasConversion<string>().HasMaxLength(20);

            // Six decimal places, not two: a rate is a fraction, and some
            // districts levy in eighths of a percent, which is 0.00125.
            rate.Property(x => x.Rate).HasPrecision(9, 6);
            rate.Property(x => x.AdministrativeArea).HasMaxLength(64);
            rate.Property(x => x.County).HasMaxLength(100);
            rate.Property(x => x.PostalKey).HasMaxLength(16);
            rate.Property(x => x.SourceUri).HasMaxLength(500).IsRequired();
        });

        modelBuilder.Entity<JurisdictionRule>(rule =>
        {
            rule.ToTable("JurisdictionRules");
            rule.HasKey(x => x.Id);
            rule.Property(x => x.Id).ValueGeneratedNever();
            rule.Property(x => x.RuleKey).HasMaxLength(64).IsRequired();
            rule.Property(x => x.Value).HasMaxLength(200).IsRequired();
            rule.HasIndex(x => new { x.PackRowId, x.RuleKey }).IsUnique();
        });
    }
}
