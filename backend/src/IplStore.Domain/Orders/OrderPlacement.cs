namespace IplStore.Domain.Orders;

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
