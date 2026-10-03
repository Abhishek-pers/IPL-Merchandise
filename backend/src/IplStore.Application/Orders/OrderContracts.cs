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
    string Currency);

public sealed record OrderLineDto(
    Guid ProductId,
    string Sku,
    string ProductName,
    string FranchiseName,
    string CategoryName,
    decimal UnitPrice,
    int Quantity,
    decimal LineTotal);

public sealed record OrderDetailsDto(
    Guid Id,
    string OrderNumber,
    OrderStatus Status,
    DateTimeOffset PlacedAt,
    int ItemCount,
    PriceSummaryDto Price,
    IReadOnlyList<OrderLineDto> Lines);

/// <summary>
/// Result of a checkout. <see cref="IsReplay"/> is true when the idempotency key had already
/// been used and the ORIGINAL order is being returned (HTTP 200 instead of 201).
/// </summary>
public sealed record CheckoutResult(OrderDetailsDto Order, bool IsReplay);
