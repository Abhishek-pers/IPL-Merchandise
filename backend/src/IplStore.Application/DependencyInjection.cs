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

        // Strategy: swap for another IPricingPolicy (festive sale, B2B ...) here only.
        services.AddSingleton<IPricingPolicy, StandardPricingPolicy>();

        services.AddScoped<ICatalogService, CatalogService>();
        services.AddScoped<ICartService, CartService>();
        services.AddScoped<ICheckoutService, CheckoutService>();
        services.AddScoped<IOrderService, OrderService>();
        services.AddScoped<IPaymentService, PaymentService>();
        services.AddScoped<ICustomerService, CustomerService>();

        return services;
    }
}
