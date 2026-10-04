using IplStore.Domain.Orders;

namespace IplStore.Application.Pricing;

/// <summary>A line to be priced: current unit price x quantity.</summary>
public sealed record PricingLine(Guid ProductId, decimal UnitPrice, int Quantity)
{
    public decimal LineTotal => UnitPrice * Quantity;
}

/// <summary>
/// Parameter object for pricing. New inputs (coupon code, shipping region, loyalty tier)
/// are added HERE as properties, so <see cref="IPricingPolicy.Calculate"/> never changes shape.
/// </summary>
public sealed record PricingRequest(IReadOnlyList<PricingLine> Lines);

/// <summary>
/// What cart and checkout depend on: how a basket is turned into money. Both use the same
/// instance, so the customer is never shown a price that checkout won't honour. In DI this is
/// <see cref="PricingPolicySelector"/>, which delegates to the best <see cref="IPricingStrategy"/>.
/// </summary>
public interface IPricingPolicy
{
    PriceBreakdown Calculate(PricingRequest request);
}
