// Copyright (c) 2026 The DealerFOSS contributors.
// SPDX-License-Identifier: AGPL-3.0-or-later
//
// Overview: Purpose, File Design, and Engineering
//   IntegrationMappingTests — ADR-021, and the two payload shapes that break mapping.
//
// Usage:
//   Runs with the normal test suite; no infrastructure required.
//
// Coding Instructions:
//   Every assertion of the form "should not be zero" is load-bearing. The
//   natural implementation of a coercion pass returns 0 for an unparseable
//   amount and a fixed date for an unparseable date, because then the caller
//   always has something to write. Both are indistinguishable from real
//   values downstream, which is the whole reason ADR-021 exists — so a test
//   that only checked "it did not throw" would pass against the bug.

using FluentAssertions;
using DealerFOSS.Integrations;

namespace DealerFOSS.UnitTests;

public sealed class IntegrationMappingTests
{
    // --- Amounts ----------------------------------------------------------

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("N/A")]
    [InlineData("1,2,3.4.5")]
    public void An_amount_that_cannot_be_read_is_absent_and_never_zero(string raw)
    {
        var value = Coerce.Amount(raw, 0m, 1_000_000m, "deal.frontGross");

        value.HasValue.Should().BeFalse();
        value.Warning!.Kind.Should().Be(MappingWarningKind.Unparseable);

        // The point of the whole exercise: a zero here is a plausible sale
        // amount that survives every downstream check.
        var readingItAnyway = () => value.Value;
        readingItAnyway.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void An_amount_outside_the_range_is_absent_and_keeps_the_original_text()
    {
        var value = Coerce.Amount("99999999999", 0m, 1_000_000m, "deal.frontGross");

        value.HasValue.Should().BeFalse();
        value.Warning!.Kind.Should().Be(MappingWarningKind.OutOfRange);
        value.Warning!.Raw.Should().Be("99999999999", "the raw text is the only way to chase it");
    }

    [Fact]
    public void An_amount_that_fits_arrives_with_no_warning_at_all()
    {
        var value = Coerce.Amount("1250.00", 0m, 1_000_000m, "deal.frontGross");

        value.HasValue.Should().BeTrue();
        value.Value.Should().Be(1250.00m);
        value.Warning.Should().BeNull();
    }

    // --- Dates ------------------------------------------------------------

    [Fact]
    public void A_date_that_cannot_be_read_is_absent_and_never_a_sentinel()
    {
        var value = Coerce.Date("0000-00-00", 1900, 2100, "deal.contractDate");

        value.HasValue.Should().BeFalse();

        // A sentinel date gives a whole dealership's records one delivery date,
        // which is a report nobody questions until it is a legal problem.
        value.Warning!.Kind.Should().Be(MappingWarningKind.Unparseable);
    }

    [Fact]
    public void A_date_outside_the_plausible_years_is_absent()
    {
        var value = Coerce.Date("1899-12-31", 1900, 2100, "deal.contractDate");

        value.HasValue.Should().BeFalse();
        value.Warning!.Kind.Should().Be(MappingWarningKind.OutOfRange);
    }

    // --- Text -------------------------------------------------------------

    [Fact]
    public void Free_text_that_is_too_long_is_truncated_and_the_loss_is_recorded()
    {
        var value = Coerce.Text(new string('x', 300), 100, "customer.addressLine1");

        value.HasValue.Should().BeTrue();
        value.Value.Should().HaveLength(100);
        value.Warning!.Kind.Should().Be(MappingWarningKind.Truncated);
        value.Warning!.Raw.Should().HaveLength(300);
    }

    [Fact]
    public void A_key_that_is_too_long_is_refused_rather_than_truncated()
    {
        // A truncated key matches the wrong record instead of failing, which is
        // strictly worse than having no value: the record looks correct.
        var value = Coerce.Text("VIN-THAT-IS-FAR-TOO-LONG", 10, "vehicle.vin", isKey: true);

        value.HasValue.Should().BeFalse();
        value.Warning!.Kind.Should().Be(MappingWarningKind.KeyTooLong);
    }

    [Fact]
    public void A_key_that_fits_is_kept_whole()
    {
        var value = Coerce.Text("1HGCM82633A", 17, "vehicle.vin", isKey: true);

        value.HasValue.Should().BeTrue();
        value.Value.Should().Be("1HGCM82633A");
        value.Warning.Should().BeNull();
    }

    // --- Parallel arrays --------------------------------------------------

    [Fact]
    public void Columns_of_equal_length_report_their_row_count()
    {
        var aligned = ColumnSet.Align(new Dictionary<string, int>(StringComparer.Ordinal)
        {
            ["payType"] = 3,
            ["roSale"] = 3,
        });

        aligned.IsSuccess.Should().BeTrue();
        aligned.Value.Should().Be(3);
    }

    [Fact]
    public void Columns_of_different_lengths_quarantine_and_name_the_lengths()
    {
        // The real failure this prevents: reading three op codes against two
        // descriptions gives one labour line another line's hours, and the
        // record looks entirely reasonable afterwards.
        var aligned = ColumnSet.Align(new Dictionary<string, int>(StringComparer.Ordinal)
        {
            ["opCode"] = 3,
            ["opDescription"] = 2,
        });

        aligned.IsFailure.Should().BeTrue();
        aligned.Error.Code.Should().Be("integration.columns_misaligned");
        aligned.Error.Message.Should().Contain("opCode=3").And.Contain("opDescription=2");
    }

    // --- Fixed arity ------------------------------------------------------

    [Fact]
    public void Six_fees_into_five_slots_is_a_refusal_that_says_how_many_would_be_lost()
    {
        var fit = Slots.Fit("fee slots", available: 5, needed: 6);

        fit.IsFailure.Should().BeTrue();
        fit.Error.Code.Should().Be("integration.no_room_in_provider_record");
        fit.Error.Message.Should().Contain("1 would be lost");

        // A Conflict rather than a transient error, because no number of
        // retries creates a sixth slot.
        fit.Error.Type.Should().Be(DealerFOSS.Core.ErrorType.Conflict);
    }

    [Fact]
    public void What_fits_fits()
    {
        Slots.Fit("fee slots", available: 5, needed: 5).IsSuccess.Should().BeTrue();
        Slots.Fit("fee slots", available: 5, needed: 0).IsSuccess.Should().BeTrue();
    }
}
