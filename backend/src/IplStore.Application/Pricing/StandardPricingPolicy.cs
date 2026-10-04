using IplStore.Domain.Orders;
using Microsoft.Extensions.Options;

namespace IplStore.Application.Pricing;

/// <summary>
/// Default strategy: subtotal + GST on the subtotal + flat shipping below a free-shipping
/// threshold. Money is rounded to 2 decimals, half away from zero (commercial rounding).
/// <para>
/// Lowest <see cref="Priority"/> and always applies, so <see cref="PricingPolicySelector"/> falls
/// back to it when no other registered strategy applies.
/// </para>
/// <para>
/// Template Method: <see cref="Calculate"/> fixes the ORDER of the steps and the rounding; each
/// rule is a protected virtual step. A new rule (e.g. GST by category) is a subclass that
/// overrides one step (and <see cref="Priority"/> / <see cref="AppliesTo"/>), registered in DI -
/// this class does not change.
/// </para>
/// </summary>
public class StandardPricingPolicy : IPricingStrategy
{
    private readonly IOptionsMonitor<PricingOptions> _options;

    public StandardPricingPolicy(IOptionsMonitor<PricingOptions> options)
    {
        _options = options;
    }

    /// <summary>Lowest priority: the fallback when no other strategy applies.</summary>
    public virtual int Priority => 0;

    /// <summary>Always applies, so the selector always finds a strategy.</summary>
    public virtual bool AppliesTo(PricingRequest request) => true;

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

        var tax = Round(CalculateTax(subtotal, request, options));
        var shipping = Round(CalculateShipping(subtotal, request, options));

        return new PriceBreakdown(subtotal, tax, shipping, options.Currency);
    }

    /// <summary>GST on the taxable amount. Override for e.g. per-category rates. Returned unrounded.</summary>
    protected virtual decimal CalculateTax(decimal taxableAmount, PricingRequest request, PricingOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        return taxableAmount * options.TaxRate;
    }

    /// <summary>Flat fee below the free-shipping threshold. Override for e.g. region-based fees. Returned unrounded.</summary>
    protected virtual decimal CalculateShipping(decimal subtotal, PricingRequest request, PricingOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        return subtotal >= options.FreeShippingThreshold ? 0m : options.FlatShippingFee;
    }

    private static decimal Round(decimal amount) => Math.Round(amount, 2, MidpointRounding.AwayFromZero);
}
