// Copyright (c) 2026 The DealerFOSS contributors.
// SPDX-License-Identifier: AGPL-3.0-or-later
//
// Overview: Purpose, File Design, and Engineering
//   DevelopmentPacks — seeds the jurisdiction tables for a development
//   deployment: the packs that ship, plus one WORKED EXAMPLE that actually
//   resolves so the end-to-end path can be seen and driven.
//
// Usage:
//   Called by DevelopmentSeeder. Never by the application.
//
// Coding Instructions:
//   THE EXAMPLE PACK IS NOT A REAL JURISDICTION AND MUST NEVER BECOME ONE. Its
//   country is ZZ, which ISO 3166 reserves for private use and no address will
//   ever carry, and its name says so in every field a person can read. It exists
//   because the shipped Indiana pack is deliberately Draft — its basis flags are
//   unreviewed — so without this there would be nothing in a development
//   database that computes a tax, and the whole mechanism would be unobservable
//   except through tests.
//
//   IT IS DEFINED HERE, IN CODE, RATHER THAN EMBEDDED ALONGSIDE us-in.json, and
//   that is the point. An example pack shipped as a resource would be loadable
//   into production by an operator pressing "load the packs that ship", and a
//   fictional rate that resolves is a worse thing to put within reach than a
//   real one that does not. This file is only ever reached from the development
//   seeder, which Program.cs gates on IsDevelopment and an explicit flag.
//
//   MARKING IT REVIEWED HERE IS HONEST because the reviewer recorded is the
//   development administrator and the thing reviewed is a fixture. Nothing in
//   this file touches us-in.json's status: a real jurisdiction is marked
//   supported by a person who has read the source, through the admin screen, and
//   by no other route.

using Microsoft.EntityFrameworkCore;
using System.Text;
using DealerFOSS.Core;

namespace DealerFOSS.Compliance;

internal static class DevelopmentPacks
{
    /// <summary>The example pack's id, so the tenant seeder can enable it.</summary>
    public const string ExamplePackId = "zz-example";

    /// <summary>
    /// A fictional jurisdiction with a state rate and a county rate that stack,
    /// chosen so the development database demonstrates the thing most likely to
    /// be got wrong: two authorities taxing one car.
    /// </summary>
    private const string ExamplePack = """
        {
          "packId": "zz-example",
          "country": "ZZ",
          "scheme": "Manual",
          "version": 1,
          "effectiveFrom": "2020-01-01",
          "effectiveTo": null,
          "sourceUri": "https://example.invalid/not-a-real-jurisdiction",
          "fetchedAt": "2026-10-18T00:00:00Z",
          "rules": {
            "tradeInReducesBasis": true,
            "documentationFeeIsTaxable": true,
            "otherFeesAreTaxable": false
          },
          "rates": [
            {
              "jurisdictionCode": "ZZ-EX",
              "description": "Example state tax (not a real jurisdiction)",
              "rateKind": "State",
              "rate": 0.05,
              "administrativeArea": "EX",
              "county": null,
              "postalKey": null,
              "effectiveFrom": "2020-01-01",
              "effectiveTo": null,
              "sourceUri": "https://example.invalid/not-a-real-jurisdiction"
            },
            {
              "jurisdictionCode": "ZZ-EX-DEMO",
              "description": "Example county tax (not a real jurisdiction)",
              "rateKind": "County",
              "rate": 0.015,
              "administrativeArea": "EX",
              "county": "Demo",
              "postalKey": null,
              "effectiveFrom": "2020-01-01",
              "effectiveTo": null,
              "sourceUri": "https://example.invalid/not-a-real-jurisdiction"
            }
          ]
        }
        """;

    /// <summary>
    /// Migrates the jurisdiction tables and loads what a development deployment
    /// should have. Idempotent: a version already present is left alone, which is
    /// R2 rather than a convenience.
    /// </summary>
    public static async Task SeedAsync(string hostConnectionString, IClock clock, string reviewerEmail)
    {
        var options = new DbContextOptionsBuilder<JurisdictionDb>()
            .UseSqlServer(hostConnectionString)
            .Options;

        await using var db = new JurisdictionDb(options);
        await db.Database.MigrateAsync();

        // Every pack built into this binary, Draft, exactly as an operator would
        // get them. Indiana arrives with its basis flags undecided and stays
        // unusable until a person fills them in — which is the behaviour a
        // developer should see, not something to paper over here.
        foreach (var (_, content) in ShippedPacks.All())
        {
            await LoadOnceAsync(db, content, clock);
        }

        var example = await LoadOnceAsync(db, Encoding.UTF8.GetBytes(ExamplePack), clock);
        if (example is not null)
        {
            example.MarkReviewed(reviewerEmail, clock.UtcNow);
            await db.SaveChangesAsync();
        }
    }

    private static async Task<JurisdictionPack?> LoadOnceAsync(
        JurisdictionDb db,
        byte[] content,
        IClock clock)
    {
        var pack = PackDocumentReader.Read(content, clock.UtcNow);

        if (await db.Packs.AnyAsync(p => p.PackId == pack.PackId && p.Version == pack.Version))
        {
            return null;
        }

        db.Packs.Add(pack);
        await db.SaveChangesAsync();
        return pack;
    }
}
