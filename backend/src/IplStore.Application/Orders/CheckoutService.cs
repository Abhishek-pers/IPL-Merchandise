using IplStore.Application.Carts;
using IplStore.Application.Catalog;
using IplStore.Application.Common;
using IplStore.Application.Customers;
using IplStore.Application.Pricing;
using IplStore.Domain.Common;
using IplStore.Domain.Orders;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace IplStore.Application.Orders;

/// <summary>Turns the customer's cart into an order.</summary>
public interface ICheckoutService
{
    Task<CheckoutResult> PlaceOrderAsync(PlaceOrderCommand command, CancellationToken cancellationToken);
}

/// <summary>
/// Checkout algorithm (all inside ONE database transaction, retried as a whole on transient faults):
/// <list type="number">
///   <item>Lock the customer's cart row -> concurrent checkouts / cart edits for this customer queue up.</item>
///   <item>Idempotency: if an order already exists for (customer, key) return it (replay).</item>
///   <item>Reserve stock per line with an atomic conditional UPDATE, in ascending product-id
///         order so two checkouts can never deadlock on each other's rows.</item>
///   <item>Price with <see cref="IPricingPolicy"/>, snapshot product data into order lines.</item>
///   <item>Create the order, empty the cart, commit.</item>
/// </list>
/// Any failure (e.g. insufficient stock) rolls back EVERYTHING, including stock already reserved
/// for earlier lines - there is no partial order and no stock leak.
/// </summary>
public sealed class CheckoutService : ICheckoutService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICartRepository _carts;
    private readonly IProductRepository _products;
    private readonly IOrderRepository _orders;
    private readonly IOrderQueries _orderQueries;
    private readonly ICustomerRepository _customers;
    private readonly IPricingPolicy _pricing;
    private readonly IOrderNumberGenerator _orderNumbers;
    private readonly IOptionsMonitor<CheckoutOptions> _options;
    private readonly TimeProvider _time;
    private readonly ILogger<CheckoutService> _logger;

    public CheckoutService(
        IUnitOfWork unitOfWork,
        ICartRepository carts,
        IProductRepository products,
        IOrderRepository orders,
        IOrderQueries orderQueries,
        ICustomerRepository customers,
        IPricingPolicy pricing,
        IOrderNumberGenerator orderNumbers,
        IOptionsMonitor<CheckoutOptions> options,
        TimeProvider time,
        ILogger<CheckoutService> logger)
    {
        _unitOfWork = unitOfWork;
        _carts = carts;
        _products = products;
        _orders = orders;
        _orderQueries = orderQueries;
        _customers = customers;
        _pricing = pricing;
        _orderNumbers = orderNumbers;
        _options = options;
        _time = time;
        _logger = logger;
    }

    public async Task<CheckoutResult> PlaceOrderAsync(PlaceOrderCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        var idempotencyKey = ResolveIdempotencyKey(command.IdempotencyKey, _options.CurrentValue);

        var (orderId, isReplay) = await _unitOfWork.ExecuteInTransactionAsync(
            ct => PlaceOrderInTransactionAsync(command.CustomerId, idempotencyKey, ct),
            cancellationToken);

        if (isReplay)
        {
            _logger.LogInformation(
                "Idempotent replay of checkout {IdempotencyKey} for customer {CustomerId} -> order {OrderId}",
                idempotencyKey, command.CustomerId, orderId);
        }
        else
        {
            _logger.LogInformation("Order {OrderId} placed for customer {CustomerId}", orderId, command.CustomerId);
        }

        var order = await _orderQueries.GetAsync(command.CustomerId, orderId, cancellationToken)
            ?? throw new InvalidOperationException($"Order {orderId} was committed but could not be read back.");

        return new CheckoutResult(order, isReplay);
    }

    private async Task<(Guid OrderId, bool IsReplay)> PlaceOrderInTransactionAsync(
        Guid customerId,
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        var customer = await _customers.FindAsync(customerId, cancellationToken)
            ?? throw new EntityNotFoundException("Customer", customerId);

        // 1. Serialise all checkouts / cart edits of THIS customer (other customers are unaffected).
        var cart = await _carts.GetOrCreateForUpdateAsync(customerId, cancellationToken);

        // 2. Idempotency check happens AFTER the lock, so a concurrent duplicate request that
        //    was waiting on the lock now sees the committed order and replays it.
        var existing = await _orders.FindByIdempotencyKeyAsync(customerId, idempotencyKey, cancellationToken);
        if (existing is not null)
        {
            return (existing.Id, true);
        }

        if (cart.IsEmpty)
        {
            throw new DomainException(DomainErrorCodes.CartEmpty, "Your cart is empty.");
        }

        // 3. Reserve stock in a deterministic order (ascending product id) -> no deadlocks.
        var cartLines = cart.Items.OrderBy(i => i.ProductId).ToList();
        var products = (await _products.GetWithReferencesAsync(cartLines.Select(i => i.ProductId).ToList(), cancellationToken))
            .ToDictionary(p => p.Id);

        var orderLines = new List<OrderLine>(cartLines.Count);
        foreach (var item in cartLines)
        {
            if (!products.TryGetValue(item.ProductId, out var product) || !product.IsActive)
            {
                throw new DomainException(
                    DomainErrorCodes.ProductUnavailable,
                    $"Product '{item.ProductId}' is no longer available. Please remove it from your cart.");
            }

            if (!await _products.TryReserveStockAsync(product.Id, item.Quantity, cancellationToken))
            {
                throw new DomainException(
                    DomainErrorCodes.InsufficientStock,
                    $"Not enough stock for '{product.Name}'. Please reduce the quantity.");
            }

            orderLines.Add(new OrderLine(
                product.Id,
                product.Sku,
                product.Name,
                product.Franchise?.Name ?? string.Empty,
                product.Category?.Name ?? string.Empty,
                product.Price,
                item.Quantity));
        }

        // 4. Price + 5. create order and empty cart.
        var price = _pricing.Calculate(new PricingRequest(
            orderLines.Select(l => new PricingLine(l.ProductId, l.UnitPrice, l.Quantity)).ToList()));

        var now = _time.GetUtcNow();
        var order = Order.Place(new OrderPlacement(
            OrderId: Guid.NewGuid(),
            OrderNumber: _orderNumbers.Next(now),
            CustomerId: customer.Id,
            CustomerName: customer.FullName,
            CustomerEmail: customer.Email,
            IdempotencyKey: idempotencyKey,
            Lines: orderLines,
            Price: price,
            PlacedAt: now));

        _orders.Add(order);
        cart.Clear(now);

        return (order.Id, false);
    }

    internal static string ResolveIdempotencyKey(string? key, CheckoutOptions options)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            if (options.RequireIdempotencyKey)
            {
                throw new RequestValidationException(
                    "Idempotency-Key",
                    "The Idempotency-Key header is required for checkout. Send a new UUID per checkout attempt and reuse it on retries.");
            }

            return Guid.NewGuid().ToString("N");
        }

        var trimmed = key.Trim();
        if (trimmed.Length > options.MaxIdempotencyKeyLength)
        {
            throw new RequestValidationException(
                "Idempotency-Key",
                $"The Idempotency-Key must be at most {options.MaxIdempotencyKeyLength} characters.");
        }

        return trimmed;
    }
}
