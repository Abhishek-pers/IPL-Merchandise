using IplStore.Application.Carts;
using IplStore.Application.Catalog;
using IplStore.Application.Customers;
using IplStore.Application.Orders;
using IplStore.Application.Pricing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace IplStore.Application;

/// <summary>
/// Registers the Application layer. Options classes declared here are BOUND by the host
/// (composition root), keeping this layer free of configuration-provider dependencies.
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        // TryAdd: the host (or a test) may register its own TimeProvider first.
        services.TryAddSingleton(TimeProvider.System);

        // Pricing strategies: any number can be registered side by side. A new one is a class
        // implementing IPricingStrategy plus ONE line here; nothing else changes.
        services.AddPricingStrategy<StandardPricingPolicy>();   // priority 0, always applies (fallback)

        // The IPricingPolicy that cart and checkout receive (Strategy context): picks, per request,
        // the highest-priority registered strategy that applies, so preview and checkout agree.
        services.AddSingleton<IPricingPolicy, PricingPolicySelector>();

        services.AddScoped<ICatalogService, CatalogService>();
        services.AddScoped<ICartService, CartService>();
        services.AddScoped<ICheckoutService, CheckoutService>();
        services.AddScoped<IOrderService, OrderService>();
        services.AddScoped<IPaymentService, PaymentService>();
        services.AddScoped<ICustomerService, CustomerService>();

        return services;
    }

    /// <summary>
    /// Registers one pricing strategy (singleton: strategies are stateless) so a consumer can inject
    /// <list type="bullet">
    ///   <item><see cref="IPricingPolicy"/> - the selector's choice per request (what cart and checkout use);</item>
    ///   <item><c>IEnumerable&lt;IPricingStrategy&gt;</c> - every registered strategy;</item>
    ///   <item><typeparamref name="TStrategy"/> itself - one specific strategy, by its class.</item>
    /// </list>
    /// All three resolve to the SAME instance per strategy.
    /// </summary>
    public static IServiceCollection AddPricingStrategy<TStrategy>(this IServiceCollection services)
        where TStrategy : class, IPricingStrategy
    {
        services.TryAddSingleton<TStrategy>();
        services.AddSingleton<IPricingStrategy>(sp => sp.GetRequiredService<TStrategy>());
        return services;
    }
}
