namespace IplStore.Application.Pricing;

/// <summary>
/// One interchangeable pricing strategy (Strategy pattern). Any number can be registered in DI
/// (see <c>AddPricingStrategy</c>); <see cref="PricingPolicySelector"/> prices each request with the
/// highest-<see cref="Priority"/> strategy whose <see cref="AppliesTo"/> returns true. A new strategy
/// is a new class plus one registration - the selector, the other strategies and the callers do
/// not change.
/// </summary>
public interface IPricingStrategy : IPricingPolicy
{
    /// <summary>Higher wins when several strategies apply.</summary>
    int Priority { get; }

    bool AppliesTo(PricingRequest request);
}
