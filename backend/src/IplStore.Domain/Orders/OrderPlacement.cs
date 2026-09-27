namespace IplStore.Domain.Orders;

/// <summary>
/// A priced line to be written into an order. Carries a SNAPSHOT of product data so order
/// history stays correct even if the product is later renamed or repriced.
/// </summary>
public sealed record OrderLine(
    Guid ProductId,
    string Sku,
    string ProductName,
    string FranchiseName,
    string CategoryName,
    decimal UnitPrice,
    int Quantity)
{
    public decimal LineTotal => UnitPrice * Quantity;
}

/// <summary>Money totals for an order or cart, produced by a pricing policy (Strategy).</summary>
public sealed record PriceBreakdown(decimal Subtotal, decimal Tax, decimal Shipping, string Currency)
{
    public decimal Total => Subtotal + Tax + Shipping;

    public static PriceBreakdown Zero(string currency) => new(0m, 0m, 0m, currency);
}

/// <summary>
/// Parameter object for <see cref="Order.Place"/>. Adding a field (e.g. shipping address)
/// means adding a property here - callers that don't care are unaffected.
/// </summary>
public sealed record OrderPlacement(
    Guid OrderId,
    string OrderNumber,
    Guid CustomerId,
    string CustomerName,
    string CustomerEmail,
    string IdempotencyKey,
    IReadOnlyList<OrderLine> Lines,
    PriceBreakdown Price,
    DateTimeOffset PlacedAt);
