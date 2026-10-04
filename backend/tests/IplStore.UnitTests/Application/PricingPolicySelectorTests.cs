using IplStore.Application;
using IplStore.Application.Pricing;
using IplStore.Domain.Orders;
using IplStore.UnitTests.Fakes;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace IplStore.UnitTests.Application;

public sealed class PricingPolicySelectorTests
{
    private static readonly TestOptionsMonitor<PricingOptions> Options = new(new PricingOptions
    {
        Currency = "INR",
        TaxRate = 0.18m,
        FlatShippingFee = 99m,
        FreeShippingThreshold = 999m,
    });

    private static PricingRequest Basket(decimal price, int quantity) =>
        new(new[] { new PricingLine(Guid.NewGuid(), price, quantity) });

    [Fact]
    public void Highest_priority_strategy_that_applies_prices_the_request()
    {
        var selector = new PricingPolicySelector(new IPricingStrategy[]
        {
            new StandardPricingPolicy(Options),
            new FlatTestStrategy(priority: 50, appliesFrom: 1000m, total: 1m),
            new FlatTestStrategy(priority: 10, appliesFrom: 0m, total: 2m),
        });

        selector.Calculate(Basket(1500m, 1)).Total.Should().Be(1m);
    }

    [Fact]
    public void Falls_back_to_the_standard_strategy_when_no_other_applies()
    {
        var selector = new PricingPolicySelector(new IPricingStrategy[]
        {
            new FlatTestStrategy(priority: 50, appliesFrom: 1000m, total: 1m),
            new StandardPricingPolicy(Options),
        });

        var price = selector.Calculate(Basket(499m, 1));

        price.Total.Should().Be(687.82m, "499 + 18% GST + 99 shipping, the standard rules");
    }

    [Fact]
    public void Requires_at_least_one_strategy()
    {
        var act = () => new PricingPolicySelector(Array.Empty<IPricingStrategy>());

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Several_strategies_can_be_registered_and_injected_three_ways()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IOptionsMonitor<PricingOptions>>(Options);
        services.AddApplication();
        services.AddPricingStrategy<HalfPriceTestStrategy>();

        using var provider = services.BuildServiceProvider();

        // 1. The selector, which is what cart and checkout receive.
        provider.GetRequiredService<IPricingPolicy>().Should().BeOfType<PricingPolicySelector>();
        provider.GetRequiredService<IPricingPolicy>().Calculate(Basket(2000m, 1)).Subtotal.Should().Be(1000m);

        // 2. Every registered strategy.
        var all = provider.GetServices<IPricingStrategy>().ToList();
        all.Select(s => s.GetType()).Should().BeEquivalentTo(new[] { typeof(StandardPricingPolicy), typeof(HalfPriceTestStrategy) });

        // 3. One specific strategy by its class - the same instance the selector uses.
        provider.GetRequiredService<StandardPricingPolicy>().Should().BeSameAs(all.OfType<StandardPricingPolicy>().Single());
    }

    /// <summary>Returns a fixed total; applies from a given subtotal.</summary>
    private sealed class FlatTestStrategy(int priority, decimal appliesFrom, decimal total) : IPricingStrategy
    {
        public int Priority => priority;

        public bool AppliesTo(PricingRequest request) => request.Lines.Sum(l => l.LineTotal) >= appliesFrom;

        public PriceBreakdown Calculate(PricingRequest request) => new(total, 0m, 0m, "INR");
    }

    /// <summary>Halves every unit price, then prices with the standard rules; applies from 1000.</summary>
    private sealed class HalfPriceTestStrategy(StandardPricingPolicy standard) : IPricingStrategy
    {
        public int Priority => 100;

        public bool AppliesTo(PricingRequest request) => request.Lines.Sum(l => l.LineTotal) >= 1000m;

        public PriceBreakdown Calculate(PricingRequest request) =>
            standard.Calculate(new PricingRequest(request.Lines.Select(l => l with { UnitPrice = l.UnitPrice / 2 }).ToList()));
    }
}
