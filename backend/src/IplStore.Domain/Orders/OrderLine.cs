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
