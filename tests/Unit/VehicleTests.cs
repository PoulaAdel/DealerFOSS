// VehicleTests — proves the vehicle and inventory rules the rest of the system
// will rely on.
//
// Use:  runs with the normal test suite; no infrastructure required.
// Edit: the VIN rules matter most. A VIN that fails the standard shape must be
//       recordable with a reason, because refusing it outright is what pushes
//       staff into inventing a fake number — the exact data problem the rule was
//       meant to prevent.

using FluentAssertions;
using OpenDealer360.Core;
using OpenDealer360.Inventory;
using OpenDealer360.Vehicles;

namespace OpenDealer360.UnitTests;

public sealed class VehicleTests
{
    private const string GoodVin = "1HGCM82633A004352";

    [Theory]
    [InlineData("1hgcm82633a004352", "1HGCM82633A004352")]
    [InlineData(" 1HGCM8 2633A0043 52 ", "1HGCM82633A004352")]
    [InlineData("1HGCM-82633-A004352", "1HGCM82633A004352")]
    public void A_vin_is_stored_upper_case_without_the_spacing_people_add(string typed, string stored)
    {
        Vin.Normalize(typed).Should().Be(stored);
    }

    [Fact]
    public void A_seventeen_character_vin_is_well_formed()
    {
        Vin.IsWellFormed(GoodVin).Should().BeTrue();
    }

    [Theory]
    [InlineData("1HGCM82633A00435")]      // sixteen characters
    [InlineData("1HGCM82633A0043521")]    // eighteen
    [InlineData("1HGCM82633A00435I")]     // I is not a VIN character
    [InlineData("1HGCM82633A00435O")]     // nor is O
    [InlineData("1HGCM82633A00435Q")]     // nor is Q
    public void A_vin_that_is_not_the_standard_shape_is_recognised_as_such(string vin)
    {
        Vin.IsWellFormed(vin).Should().BeFalse();
    }

    [Fact]
    public void A_vehicle_reads_as_year_make_model_and_trim()
    {
        var vehicle = Vehicle.Record(Guid.NewGuid(), GoodVin, 2021, "Toyota", "RAV4", "XLE");

        vehicle.DisplayName.Should().Be("2021 Toyota RAV4 XLE");
        vehicle.HasVinException.Should().BeFalse();
    }

    [Fact]
    public void A_vehicle_with_no_trim_still_reads_sensibly()
    {
        var vehicle = Vehicle.Record(Guid.NewGuid(), GoodVin, 2021, "Toyota", "RAV4");

        vehicle.DisplayName.Should().Be("2021 Toyota RAV4",
            because: "plenty of imported records have no trim, and it must not render a double space");
    }

    [Fact]
    public void An_unusual_vin_is_refused_until_a_reason_is_given()
    {
        var withoutReason = () => Vehicle.Record(Guid.NewGuid(), "TRAILER1975A", 1975, "Wells Cargo", "Utility");

        withoutReason.Should().Throw<ArgumentException>()
            .WithMessage("*reason*", because: "the message must say how to proceed, not just refuse");
    }

    [Fact]
    public void An_unusual_vin_is_recorded_when_the_reason_is_documented()
    {
        // A pre-1981 trailer genuinely has no 17-character VIN. Refusing it would
        // make staff type a fake one, which is worse (doc 04 §4).
        var vehicle = Vehicle.Record(
            Guid.NewGuid(), "TRAILER-1975-A", 1975, "Wells Cargo", "Utility",
            vinExceptionReason: "Pre-1981 trailer; number read from the frame plate.");

        vehicle.Vin.Should().Be("TRAILER1975A");
        vehicle.HasVinException.Should().BeTrue();
        vehicle.VinExceptionReason.Should().Contain("frame plate");
    }

    [Fact]
    public void A_well_formed_vin_never_carries_an_exception_even_if_one_is_offered()
    {
        var vehicle = Vehicle.Record(
            Guid.NewGuid(), GoodVin, 2021, "Toyota", "RAV4", vinExceptionReason: "not needed");

        vehicle.HasVinException.Should().BeFalse(
            because: "an exception on a standard VIN would be noise in every later review");
    }

    [Fact]
    public void A_vehicle_needs_a_vin_or_a_reason_it_has_none()
    {
        var blank = () => Vehicle.Record(Guid.NewGuid(), "", 2021, "Toyota", "RAV4");

        blank.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData(1899)]
    [InlineData(2101)]
    public void An_impossible_model_year_is_rejected(int year)
    {
        var record = () => Vehicle.Record(Guid.NewGuid(), GoodVin, year, "Toyota", "RAV4");

        record.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void A_vehicle_needs_a_make_and_a_model()
    {
        var noMake = () => Vehicle.Record(Guid.NewGuid(), GoodVin, 2021, " ", "RAV4");
        var noModel = () => Vehicle.Record(Guid.NewGuid(), GoodVin, 2021, "Toyota", "");

        noMake.Should().Throw<ArgumentException>();
        noModel.Should().Throw<ArgumentException>();
    }

    // --- inventory units ---------------------------------------------------

    [Theory]
    [InlineData("a 1234", "A1234")]
    [InlineData("A-1234", "A1234")]
    [InlineData("  a1234 ", "A1234")]
    public void A_stock_number_is_stored_one_way_however_it_is_written(string typed, string stored)
    {
        InventoryUnit.NormalizeStockNumber(typed).Should().Be(stored);
    }

    [Fact]
    public void A_unit_needs_a_stock_number()
    {
        var blank = () => InventoryUnit.NormalizeStockNumber("   ");

        blank.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void A_received_unit_starts_incoming_with_that_move_recorded()
    {
        var unit = Receive();

        unit.Status.Should().Be(InventoryStatus.Incoming);
        unit.StatusHistory.Should().ContainSingle()
            .Which.ToStatus.Should().Be(InventoryStatus.Incoming);
        unit.StatusHistory[0].FromStatus.Should().BeNull(
            because: "a unit entering stock did not come from another status");
    }

    [Fact]
    public void A_unit_belongs_to_the_rooftop_it_was_received_at()
    {
        var rooftop = RooftopId.New();

        Receive(rooftop).RooftopId.Should().Be(rooftop);
    }

    [Fact]
    public void Every_status_change_is_added_to_the_history()
    {
        var unit = Receive();
        var now = DateTimeOffset.UtcNow;

        unit.ChangeStatus(InventoryStatus.Reconditioning, now, note: "Awaiting tyres.");
        unit.ChangeStatus(InventoryStatus.Available, now.AddHours(4));

        unit.Status.Should().Be(InventoryStatus.Available);
        unit.StatusHistory.Should().HaveCount(3);
        unit.StatusHistory[1].Note.Should().Be("Awaiting tyres.");
        unit.StatusHistory[2].FromStatus.Should().Be(InventoryStatus.Reconditioning);
    }

    [Fact]
    public void A_unit_cannot_move_to_the_status_it_already_has()
    {
        var unit = Receive();

        var noOp = () => unit.ChangeStatus(InventoryStatus.Incoming, DateTimeOffset.UtcNow);

        noOp.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void A_removed_unit_cannot_come_back_by_changing_status()
    {
        // It comes back as a new unit, so its second stay has its own history and
        // its own cost.
        var unit = Receive();
        unit.ChangeStatus(InventoryStatus.Removed, DateTimeOffset.UtcNow, note: "Wholesaled.");

        var revive = () => unit.ChangeStatus(InventoryStatus.Available, DateTimeOffset.UtcNow);

        revive.Should().Throw<InvalidOperationException>()
            .WithMessage("*Receive it again*");
    }

    [Fact]
    public void A_sold_unit_can_go_back_on_the_lot_when_the_deal_falls_through()
    {
        var unit = Receive();
        unit.ChangeStatus(InventoryStatus.Available, DateTimeOffset.UtcNow);
        unit.ChangeStatus(InventoryStatus.Sold, DateTimeOffset.UtcNow);

        unit.ChangeStatus(InventoryStatus.Available, DateTimeOffset.UtcNow, note: "Financing declined.");

        unit.Status.Should().Be(InventoryStatus.Available);
        unit.StatusHistory.Should().HaveCount(4,
            because: "an unwound deal is a recorded move, not an erased one");
    }

    [Fact]
    public void A_refused_move_says_which_moves_are_available()
    {
        var unit = Receive();

        var straightToSold = () => unit.ChangeStatus(InventoryStatus.Sold, DateTimeOffset.UtcNow);

        straightToSold.Should().Throw<InvalidOperationException>()
            .WithMessage("*Available*", because: "the message must tell the user what they can do instead");
    }

    [Fact]
    public void Cost_keeps_its_currency()
    {
        var unit = Receive();

        unit.SetCost(new Money(24500m, "usd"));

        unit.Cost!.Value.Amount.Should().Be(24500m);
        unit.Cost!.Value.Currency.Should().Be("USD");
    }

    [Fact]
    public void A_unit_with_no_recorded_cost_has_no_money_value()
    {
        Receive().Cost.Should().BeNull(
            because: "an unknown cost is not zero, and reporting must be able to tell them apart");
    }

    private static InventoryUnit Receive(RooftopId? rooftop = null) =>
        InventoryUnit.Receive(
            Guid.NewGuid(),
            Guid.NewGuid(),
            rooftop ?? RooftopId.New(),
            "A1001",
            DateTimeOffset.UtcNow);
}
