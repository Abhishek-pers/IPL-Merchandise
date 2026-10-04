using System.Linq.Expressions;
using IplStore.Domain.Orders;

namespace IplStore.Application.Orders;

/// <summary>
/// Checkout command. <see cref="IdempotencyKey"/> comes from the <c>Idempotency-Key</c> HTTP
/// header: the client generates it once per checkout attempt and re-sends the SAME key on
/// retries, so a timeout + retry can never create two orders.
/// </summary>
public sealed record PlaceOrderCommand(Guid CustomerId, string? IdempotencyKey);

/// <summary>Pay for a placed order. <see cref="SimulateFailure"/> drives the dummy gateway's outcome.</summary>
public sealed record PayOrderCommand(Guid CustomerId, Guid OrderId, bool SimulateFailure);

public sealed record PriceSummaryDto(decimal Subtotal, decimal Tax, decimal Shipping, decimal Total, string Currency)
{
    public static PriceSummaryDto From(PriceBreakdown price)
    {
        ArgumentNullException.ThrowIfNull(price);
        return new PriceSummaryDto(price.Subtotal, price.Tax, price.Shipping, price.Total, price.Currency);
    }
}

public sealed record OrderSummaryDto(
    Guid Id,
    string OrderNumber,
    OrderStatus Status,
    DateTimeOffset PlacedAt,
    int ItemCount,
    decimal Total,
    string Currency)
{
    /// <summary>
    /// The ONE mapping from an order to a list row. An expression (not a method) so EF Core
    /// translates it to SQL and reads only these columns; in-memory callers use <see cref="From"/>.
    /// </summary>
    public static readonly Expression<Func<Order, OrderSummaryDto>> Projection =
        o => new OrderSummaryDto(o.Id, o.OrderNumber, o.Status, o.PlacedAt, o.ItemCount, o.Total, o.Price.Currency);

    private static readonly Func<Order, OrderSummaryDto> Compiled = Projection.Compile();

    public static OrderSummaryDto From(Order order) => Compiled(order);
}

public sealed record OrderLineDto(
    Guid ProductId,
    string Sku,
    string ProductName,
    string FranchiseName,
    string CategoryName,
    decimal UnitPrice,
    int Quantity,
    decimal LineTotal)
{
    public static OrderLineDto From(OrderItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        return new OrderLineDto(
            item.ProductId, item.Sku, item.ProductName, item.FranchiseName, item.CategoryName, item.UnitPrice, item.Quantity, item.LineTotal);
    }
}

public sealed record OrderDetailsDto(
    Guid Id,
    string OrderNumber,
    OrderStatus Status,
    DateTimeOffset PlacedAt,
    int ItemCount,
    PriceSummaryDto Price,
    IReadOnlyList<OrderLineDto> Lines)
{
    /// <summary>The ONE mapping from an order (with its items loaded) to the details response.</summary>
    public static OrderDetailsDto From(Order order)
    {
        ArgumentNullException.ThrowIfNull(order);
        return new OrderDetailsDto(
            order.Id,
            order.OrderNumber,
            order.Status,
            order.PlacedAt,
            order.ItemCount,
            PriceSummaryDto.From(order.Price),
            order.Items.OrderBy(i => i.ProductName, StringComparer.Ordinal).Select(OrderLineDto.From).ToList());
    }
}

/// <summary>
/// Result of a checkout. <see cref="IsReplay"/> is true when the idempotency key had already
/// been used and the ORIGINAL order is being returned (HTTP 200 instead of 201).
/// </summary>
public sealed record CheckoutResult(OrderDetailsDto Order, bool IsReplay);
