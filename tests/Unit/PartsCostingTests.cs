// PartsCostingTests — the three costing methods, and the property that makes
// switching between them safe.
//
// Use:  runs with the normal test suite; no database needed.
// Edit: the test that matters most is
//       Every_method_reads_the_same_layers_so_switching_needs_no_migration. It is
//       the justification for keeping receipt layers even when an organization
//       only ever uses moving average — without them, switching to FIFO later
//       would silently produce wrong costs from the day it was switched.

using FluentAssertions;
using DealerFOSS.Core;
using DealerFOSS.Parts;
using Xunit;

namespace DealerFOSS.UnitTests;

public sealed class PartsCostingTests
{
    private static readonly RooftopId Rooftop = new(Guid.NewGuid());
    private static readonly Guid PartId = Guid.NewGuid();
    private static readonly DateTimeOffset Day = new(2026, 8, 1, 9, 0, 0, TimeSpan.Zero);

    /// <summary>Two deliveries: 10 at 5.00, then 10 at 9.00. Average is 7.00.</summary>
    private static List<StockReceipt> TwoLayers() =>
    [
        Layer(quantity: 10m, cost: 5.00m, daysLater: 0),
        Layer(quantity: 10m, cost: 9.00m, daysLater: 1),
    ];

    [Fact]
    public void Moving_average_is_the_value_on_the_shelf_over_the_quantity_on_it()
    {
        PartsCosting.CostOf(PartsCostingMethod.MovingAverage, TwoLayers(), 4m)
            .Should().Be(28.00m, because: "4 at an average of 7.00");
    }

    [Fact]
    public void Last_cost_uses_the_newest_delivery()
    {
        PartsCosting.CostOf(PartsCostingMethod.LastCost, TwoLayers(), 4m)
            .Should().Be(36.00m, because: "4 at the most recent 9.00");
    }

    [Fact]
    public void Fifo_consumes_the_oldest_delivery_first()
    {
        PartsCosting.CostOf(PartsCostingMethod.Fifo, TwoLayers(), 4m)
            .Should().Be(20.00m, because: "4 still come out of the old 5.00 layer");
    }

    [Fact]
    public void Fifo_crosses_into_the_next_layer_when_the_oldest_runs_out()
    {
        // 10 at 5.00 exhausts the first layer, then 2 at 9.00.
        PartsCosting.CostOf(PartsCostingMethod.Fifo, TwoLayers(), 12m)
            .Should().Be(68.00m, because: "50.00 from the old layer plus 18.00 from the new one");
    }

    [Fact]
    public void Every_method_reads_the_same_layers_so_switching_needs_no_migration()
    {
        // The reason StockReceipt exists even for an organization that only ever
        // uses moving average. If stock were a running total plus an average,
        // switching to FIFO would have no history to consume and would quietly
        // produce wrong costs from that day on.
        var layers = TwoLayers();

        foreach (var method in Enum.GetValues<PartsCostingMethod>())
        {
            PartsCosting.CostOf(method, layers, 4m)
                .Should().NotBeNull(because: $"{method} must work off the same data as the others");
        }
    }

    [Fact]
    public void Not_enough_stock_has_no_cost_rather_than_a_guessed_one()
    {
        // Null is the caller's cue to refuse. A workshop that can sell parts it
        // does not have has no stock figure at all.
        PartsCosting.CostOf(PartsCostingMethod.MovingAverage, TwoLayers(), 21m)
            .Should().BeNull();
    }

    [Fact]
    public void An_empty_shelf_has_no_cost_for_anything_but_nothing()
    {
        PartsCosting.CostOf(PartsCostingMethod.MovingAverage, [], 1m).Should().BeNull();
        PartsCosting.CostOf(PartsCostingMethod.MovingAverage, [], 0m).Should().Be(0m);
    }

    [Fact]
    public void An_average_of_an_empty_shelf_is_zero_rather_than_a_division_by_it()
    {
        var spent = Layer(quantity: 5m, cost: 4.00m, daysLater: 0);
        spent.Take(5m);

        PartsCosting.MovingAverageOf([spent]).Should().Be(0m);
    }

    [Fact]
    public void Last_cost_is_a_fact_about_the_last_purchase_not_about_the_shelf()
    {
        // The newest layer is spent, but what was last paid for the part has not
        // changed — that is what "last cost" means.
        var layers = TwoLayers();
        layers[1].Take(10m);

        PartsCosting.LastCostOf(layers).Should().Be(9.00m);
    }

    [Fact]
    public void Taking_from_a_layer_never_goes_below_nothing()
    {
        var layer = Layer(quantity: 3m, cost: 2.00m, daysLater: 0);

        layer.Take(10m).Should().Be(3m, because: "a layer can only give what it has");
        layer.RemainingQuantity.Should().Be(0m);
    }

    [Fact]
    public void Returning_stock_cannot_put_back_more_than_came_in()
    {
        var layer = Layer(quantity: 3m, cost: 2.00m, daysLater: 0);
        layer.Take(3m);

        layer.Return(100m);

        layer.RemainingQuantity.Should().Be(3m, because: "a layer cannot grow beyond its delivery");
    }

    [Fact]
    public void A_delivery_of_nothing_is_refused()
    {
        var receive = () => Layer(quantity: 0m, cost: 1m, daysLater: 0);

        receive.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Oldest_first_ignores_layers_with_nothing_left()
    {
        var layers = TwoLayers();
        layers[0].Take(10m);

        PartsCosting.Oldest(layers).Should().ContainSingle()
            .Which.UnitCostAmount.Should().Be(9.00m);
    }

    [Fact]
    public void A_part_number_is_matched_however_it_was_typed()
    {
        // Suppliers and staff write the same number a dozen ways, and two
        // catalogue rows for one component is what makes a parts department stop
        // trusting the figures.
        Part.Normalize("mz-690411").Should().Be("MZ690411");
        Part.Normalize("MZ 690 411").Should().Be("MZ690411");
        Part.Normalize(" MZ690411 ").Should().Be("MZ690411");
    }

    private static StockReceipt Layer(decimal quantity, decimal cost, int daysLater) =>
        new(Guid.NewGuid(), PartId, Rooftop, quantity, new Money(cost, "USD"), Day.AddDays(daysLater), null);
}
