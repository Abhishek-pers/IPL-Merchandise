namespace IplStore.Domain.Orders;

/// <summary>Money totals for an order or cart, produced by a pricing policy (Strategy).</summary>
public sealed record PriceBreakdown(decimal Subtotal, decimal Tax, decimal Shipping, string Currency)
{
    public decimal Total => Subtotal + Tax + Shipping;

    public static PriceBreakdown Zero(string currency) => new(0m, 0m, 0m, currency);
}
