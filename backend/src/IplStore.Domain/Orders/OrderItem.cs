namespace IplStore.Domain.Orders;

/// <summary>Immutable, denormalised line of a placed order.</summary>
public sealed class OrderItem
{
    private OrderItem()
    {
        // Required by the ORM.
    }

    internal OrderItem(Guid id, Guid orderId, Guid customerId, OrderLine line)
    {
        Id = id;
        OrderId = orderId;
        CustomerId = customerId;
        ProductId = line.ProductId;
        Sku = line.Sku;
        ProductName = line.ProductName;
        FranchiseName = line.FranchiseName;
        CategoryName = line.CategoryName;
        UnitPrice = line.UnitPrice;
        Quantity = line.Quantity;
        LineTotal = line.LineTotal;
    }

    public Guid Id { get; private set; }

    public Guid OrderId { get; private set; }

    /// <summary>Denormalised distribution / shard key (docs/06).</summary>
    public Guid CustomerId { get; private set; }

    public Guid ProductId { get; private set; }

    public string Sku { get; private set; } = string.Empty;

    public string ProductName { get; private set; } = string.Empty;

    public string FranchiseName { get; private set; } = string.Empty;

    public string CategoryName { get; private set; } = string.Empty;

    public decimal UnitPrice { get; private set; }

    public int Quantity { get; private set; }

    public decimal LineTotal { get; private set; }
}
