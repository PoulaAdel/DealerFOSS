// Copyright (c) 2026 The DealerFOSS contributors.
// SPDX-License-Identifier: AGPL-3.0-or-later
//
// Overview: Purpose, File Design, and Engineering
//   JurisdictionPackTests — the pack file format, the matcher, and the review
//   gate, exercised without a database.
//
// Usage:
//   dotnet test
//
// Coding Instructions:
//   These assert the REFUSALS as hard as the successes, because every one of
//   them exists to stop a wrong number reaching an invoice wearing a rate
//   table's name. A test that only proved a good pack loads would leave the
//   whole of ADR-024 R1 unchecked.

using System.Text;
using DealerFOSS.Compliance;

namespace DealerFOSS.UnitTests;

public sealed class JurisdictionPackTests
{
    private static readonly DateTimeOffset LoadedAt = new(2026, 10, 18, 9, 0, 0, TimeSpan.Zero);

    private static byte[] Pack(string rules = "null", string rates = "[]", string version = "1") =>
        Encoding.UTF8.GetBytes($$"""
            {
              "packId": "zz-test",
              "country": "ZZ",
              "scheme": "Manual",
              "version": {{version}},
              "effectiveFrom": "2026-01-01",
              "sourceUri": "https://example.invalid/source",
              "fetchedAt": "2026-10-18T00:00:00Z",
              "rules": {{rules}},
              "rates": {{rates}}
            }
            """);

    private const string AllThreeFlags = """
        { "tradeInReducesBasis": true, "documentationFeeIsTaxable": true, "otherFeesAreTaxable": false }
        """;

    private static string Rate(
        string code = "ZZ-T",
        decimal rate = 0.05m,
        string kind = "State",
        string? area = "EX",
        string? county = null,
        string? postal = null) =>
        $$"""
          [{
            "jurisdictionCode": "{{code}}",
            "description": "Test tax",
            "rateKind": "{{kind}}",
            "rate": {{rate.ToString(System.Globalization.CultureInfo.InvariantCulture)}},
            "administrativeArea": {{Json(area)}},
            "county": {{Json(county)}},
            "postalKey": {{Json(postal)}},
            "effectiveFrom": "2026-01-01",
            "sourceUri": "https://example.invalid/rate"
          }]
          """;

    private static string Json(string? value) => value is null ? "null" : $"\"{value}\"";

    [Fact]
    public void A_pack_loads_as_draft_however_complete_it_looks()
    {
        var pack = PackDocumentReader.Read(Pack(AllThreeFlags, Rate()), LoadedAt);

        // The one property that cannot be influenced by the file. A pack that
        // could arrive Supported would be a pack nobody reviewed wearing the
        // label that says somebody did.
        Assert.Equal(PackStatus.Draft, pack.Status);
        Assert.Null(pack.ReviewedBy);
    }

    [Fact]
    public void An_unknown_rule_key_is_refused_rather_than_ignored()
    {
        var content = Pack("""
            { "tradeInReducesBasis": true, "leaseTaxedOnPayment": true }
            """);

        var failure = Assert.Throws<PackFormatException>(() => PackDocumentReader.Read(content, LoadedAt));

        // Naming the key is the point: a pack author who wrote it believes it
        // does something, and silence would let them keep believing that.
        Assert.Contains("leaseTaxedOnPayment", failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_percentage_written_where_a_fraction_belongs_is_refused()
    {
        // 7 instead of 0.07 would tax a $30,000 car $210,000.
        var failure = Assert.Throws<PackFormatException>(
            () => PackDocumentReader.Read(Pack(AllThreeFlags, Rate(rate: 7m)), LoadedAt));

        Assert.Contains("0.07", failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_source_that_nobody_recorded_reading_is_refused()
    {
        var content = Encoding.UTF8.GetBytes("""
            {
              "packId": "zz-test", "country": "ZZ", "scheme": "Manual", "version": 1,
              "effectiveFrom": "2026-01-01",
              "sourceUri": "https://example.invalid/source"
            }
            """);

        var failure = Assert.Throws<PackFormatException>(() => PackDocumentReader.Read(content, LoadedAt));
        Assert.Contains("fetchedAt", failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_pack_cannot_be_marked_supported_while_a_basis_flag_is_undecided()
    {
        var pack = PackDocumentReader.Read(
            Pack("""{ "tradeInReducesBasis": true }""", Rate()),
            LoadedAt);

        var failure = Assert.Throws<InvalidOperationException>(
            () => pack.MarkReviewed("reviewer@example.invalid", LoadedAt));

        // Both remaining flags named, so the reviewer is told what to decide
        // rather than only that something is missing.
        Assert.Contains("documentationFeeIsTaxable", failure.Message, StringComparison.Ordinal);
        Assert.Contains("otherFeesAreTaxable", failure.Message, StringComparison.Ordinal);
        Assert.Equal(PackStatus.Draft, pack.Status);
    }

    [Fact]
    public void The_shipped_indiana_pack_is_sourced_and_deliberately_unreviewable()
    {
        var indiana = ShippedPacks.All().Single(p => p.Name.Contains("us-in", StringComparison.Ordinal));
        var pack = PackDocumentReader.Read(indiana.Content, LoadedAt);

        Assert.Equal("us-in", pack.PackId);
        Assert.Equal("US", pack.Country);
        Assert.Equal(0.07m, Assert.Single(pack.Rates).Rate);
        Assert.Contains("in.gov", pack.SourceUri, StringComparison.Ordinal);

        // The whole point of how it ships: the rate is sourced, what the rate
        // applies TO is not, and the product refuses to pretend otherwise.
        Assert.Equal(3, pack.MissingRequiredRules().Count);
        Assert.Throws<InvalidOperationException>(
            () => pack.MarkReviewed("reviewer@example.invalid", LoadedAt));
    }

    [Fact]
    public void A_reviewed_pack_records_who_and_when()
    {
        var pack = PackDocumentReader.Read(Pack(AllThreeFlags, Rate()), LoadedAt);

        pack.MarkReviewed("reviewer@example.invalid", LoadedAt);

        Assert.Equal(PackStatus.Supported, pack.Status);
        Assert.Equal("reviewer@example.invalid", pack.ReviewedBy);
        Assert.Equal(LoadedAt, pack.ReviewedAt);
    }

    [Fact]
    public void The_hash_follows_the_bytes_and_nothing_else()
    {
        var once = PackDocumentReader.Read(Pack(AllThreeFlags, Rate()), LoadedAt);
        var again = PackDocumentReader.Read(Pack(AllThreeFlags, Rate()), LoadedAt.AddDays(1));

        // Same bytes, different load time: the approval travels with the content,
        // not with the occasion.
        Assert.Equal(once.ContentHash, again.ContentHash);

        var edited = PackDocumentReader.Read(Pack(AllThreeFlags, Rate(rate: 0.06m)), LoadedAt);
        Assert.NotEqual(once.ContentHash, edited.ContentHash);
    }

    [Theory]
    // A row that names only a state matches anywhere in it, at any county.
    [InlineData("EX", null, null, true)]
    [InlineData("EX", "Anywhere", "99999", true)]
    // Case and padding are a person typing into a deal, not a different state.
    [InlineData(" ex ", null, null, true)]
    [InlineData("OTHER", null, null, false)]
    [InlineData(null, null, null, false)]
    public void A_state_rate_matches_on_the_state_alone(
        string? area, string? county, string? postal, bool expected)
    {
        var rate = Assert.Single(PackDocumentReader.Read(Pack(AllThreeFlags, Rate()), LoadedAt).Rates);

        Assert.Equal(expected, rate.Matches(area, county, postal));
    }

    [Fact]
    public void A_county_rate_needs_the_county_as_well_as_the_state()
    {
        var rate = Assert.Single(
            PackDocumentReader.Read(
                Pack(AllThreeFlags, Rate(kind: "County", county: "Demo")),
                LoadedAt).Rates);

        Assert.True(rate.Matches("EX", "Demo", null));

        // The right state and the wrong county is the case that matters: a US
        // rate depends on both, and matching on the state alone here would
        // charge every address in the state a county's tax.
        Assert.False(rate.Matches("EX", "Elsewhere", null));
        Assert.False(rate.Matches("EX", null, null));
    }
}
