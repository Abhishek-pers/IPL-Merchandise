using IplStore.Domain.Orders;

namespace IplStore.Application.Pricing;

/// <summary>
/// Strategy context: the single <see cref="IPricingPolicy"/> that cart and checkout receive.
/// Picks the highest-priority registered strategy that applies to the request. Ties are broken by
/// registration order, so give promotions distinct priorities.
/// </summary>
public sealed class PricingPolicySelector : IPricingPolicy
{
    private readonly IReadOnlyList<IPricingStrategy> _byPriority;

    public PricingPolicySelector(IEnumerable<IPricingStrategy> strategies)
    {
        ArgumentNullException.ThrowIfNull(strategies);

        // OrderByDescending is stable: equal priorities keep their registration order.
        _byPriority = strategies.OrderByDescending(s => s.Priority).ToList();
        if (_byPriority.Count == 0)
        {
            throw new InvalidOperationException("At least one IPricingStrategy must be registered.");
        }
    }

    public PriceBreakdown Calculate(PricingRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var strategy = _byPriority.FirstOrDefault(s => s.AppliesTo(request))
            ?? throw new InvalidOperationException("No pricing strategy applies; register a default that always applies.");
        return strategy.Calculate(request);
    }
}
