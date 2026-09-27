using IplStore.Application.Pricing;
using IplStore.UnitTests.Fakes;

namespace IplStore.UnitTests.Application;

public sealed class StandardPricingPolicyTests
{
    private readonly TestOptionsMonitor<PricingOptions> _options = new(new PricingOptions
    {
        Currency = "INR",
        TaxRate = 0.18m,
        FlatShippingFee = 99m,
        FreeShippingThreshold = 999m,
    });

    private StandardPricingPolicy Policy => new(_options);

    private static PricingRequest Basket(params (decimal Price, int Qty)[] lines) =>
        new(lines.Select(l => new PricingLine(Guid.NewGuid(), l.Price, l.Qty)).ToList());

    [Fact]
    public void Below_free_shipping_threshold_charges_flat_shipping()
    {
        var price = Policy.Calculate(Basket((499m, 1)));

        price.Subtotal.Should().Be(499m);
        price.Tax.Should().Be(89.82m);
        price.Shipping.Should().Be(99m);
        price.Total.Should().Be(687.82m);
        price.Currency.Should().Be("INR");
    }

    [Fact]
    public void At_or_above_threshold_ships_free()
    {
        var price = Policy.Calculate(Basket((999m, 1)));

        price.Shipping.Should().Be(0m);
    }

    [Fact]
    public void Tax_is_rounded_half_away_from_zero_to_two_decimals()
    {
        // 0.25 * 0.18 = 0.045 -> 0.05 (banker's rounding would give 0.04)
        var price = Policy.Calculate(Basket((0.25m, 1)));

        price.Tax.Should().Be(0.05m);
    }

    [Fact]
    public void Empty_basket_costs_nothing_not_even_shipping()
    {
        var price = Policy.Calculate(Basket());

        price.Total.Should().Be(0m);
    }

    [Fact]
    public void Configuration_changes_apply_to_the_next_calculation_without_restart()
    {
        _options.CurrentValue = new PricingOptions { Currency = "INR", TaxRate = 0m, FlatShippingFee = 0m, FreeShippingThreshold = 0m };

        var price = Policy.Calculate(Basket((100m, 2)));

        price.Total.Should().Be(200m);
    }
}
