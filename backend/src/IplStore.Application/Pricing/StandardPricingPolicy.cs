using IplStore.Domain.Orders;
using Microsoft.Extensions.Options;

namespace IplStore.Application.Pricing;

/// <summary>
/// Default policy: subtotal + GST on the subtotal + flat shipping below a free-shipping
/// threshold. Money is rounded to 2 decimals, half away from zero (commercial rounding).
/// </summary>
public sealed class StandardPricingPolicy : IPricingPolicy
{
    private readonly IOptionsMonitor<PricingOptions> _options;

    public StandardPricingPolicy(IOptionsMonitor<PricingOptions> options)
    {
        _options = options;
    }

    public PriceBreakdown Calculate(PricingRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        // Snapshot once: a config reload mid-calculation must not mix old and new values.
        var options = _options.CurrentValue;

        var subtotal = Round(request.Lines.Sum(l => l.LineTotal));
        if (subtotal == 0m)
        {
            return PriceBreakdown.Zero(options.Currency);
        }

        var tax = Round(subtotal * options.TaxRate);
        var shipping = subtotal >= options.FreeShippingThreshold ? 0m : Round(options.FlatShippingFee);

        return new PriceBreakdown(subtotal, tax, shipping, options.Currency);
    }

    private static decimal Round(decimal amount) => Math.Round(amount, 2, MidpointRounding.AwayFromZero);
}
