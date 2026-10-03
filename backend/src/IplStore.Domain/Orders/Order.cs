using IplStore.Domain.Common;

namespace IplStore.Domain.Orders;

/// <summary>
/// A placed order (aggregate root). Created only through <see cref="Place"/>, which enforces
/// the invariants; afterwards the lines are immutable.
/// </summary>
public sealed class Order
{
    private readonly List<OrderItem> _items = new();

    private Order()
    {
        // Required by the ORM.
    }

    public Guid Id { get; private set; }

    public string OrderNumber { get; private set; } = string.Empty;

    public Guid CustomerId { get; private set; }

    /// <summary>Client-supplied key; UNIQUE per customer so retries never create duplicates.</summary>
    public string IdempotencyKey { get; private set; } = string.Empty;

    public OrderStatus Status { get; private set; }

    public string Currency { get; private set; } = string.Empty;

    public decimal Subtotal { get; private set; }

    public decimal Tax { get; private set; }

    public decimal Shipping { get; private set; }

    public decimal Total { get; private set; }

    /// <summary>Denormalised total units, so the order-history list needs no join.</summary>
    public int ItemCount { get; private set; }

    public string CustomerName { get; private set; } = string.Empty;

    public string CustomerEmail { get; private set; } = string.Empty;

    public DateTimeOffset PlacedAt { get; private set; }

    public IReadOnlyCollection<OrderItem> Items => _items.AsReadOnly();

    /// <summary>Factory: validates the placement and builds the aggregate.</summary>
    public static Order Place(OrderPlacement placement)
    {
        ArgumentNullException.ThrowIfNull(placement);
        Guard.NotEmpty(placement.OrderId, nameof(placement.OrderId));
        Guard.NotEmpty(placement.CustomerId, nameof(placement.CustomerId));
        Guard.NotBlank(placement.OrderNumber, nameof(placement.OrderNumber), 32);
        Guard.NotBlank(placement.IdempotencyKey, nameof(placement.IdempotencyKey), 100);

        if (placement.Lines is null || placement.Lines.Count == 0)
        {
            throw new DomainException(DomainErrorCodes.OrderHasNoLines, "An order must contain at least one line.");
        }

        if (placement.Lines.Select(l => l.ProductId).Distinct().Count() != placement.Lines.Count)
        {
            throw new DomainException(DomainErrorCodes.OrderDuplicateLines, "An order cannot contain the same product twice.");
        }

        foreach (var line in placement.Lines)
        {
            Guard.Positive(line.Quantity, nameof(line.Quantity));
            Guard.NotNegative(line.UnitPrice, nameof(line.UnitPrice));
        }

        var linesSubtotal = placement.Lines.Sum(l => l.LineTotal);
        if (linesSubtotal != placement.Price.Subtotal)
        {
            throw new DomainException(
                DomainErrorCodes.OrderPricingMismatch,
                $"Price subtotal {placement.Price.Subtotal} does not match the sum of lines {linesSubtotal}.");
        }

        var order = new Order
        {
            Id = placement.OrderId,
            OrderNumber = placement.OrderNumber,
            CustomerId = placement.CustomerId,
            IdempotencyKey = placement.IdempotencyKey,
            Status = OrderStatus.Placed,
            Currency = placement.Price.Currency,
            Subtotal = placement.Price.Subtotal,
            Tax = placement.Price.Tax,
            Shipping = placement.Price.Shipping,
            Total = placement.Price.Total,
            ItemCount = placement.Lines.Sum(l => l.Quantity),
            CustomerName = placement.CustomerName,
            CustomerEmail = placement.CustomerEmail,
            PlacedAt = placement.PlacedAt,
        };

        foreach (var line in placement.Lines)
        {
            order._items.Add(new OrderItem(Guid.NewGuid(), order.Id, order.CustomerId, line));
        }

        return order;
    }

    /// <summary>Placed -> Paid. Idempotent: marking an already-paid order again is a no-op.</summary>
    public void MarkPaid()
    {
        if (Status == OrderStatus.Paid)
        {
            return;
        }

        if (Status != OrderStatus.Placed)
        {
            throw new DomainException(DomainErrorCodes.OrderNotPayable, $"Order {OrderNumber} is {Status} and cannot be paid.");
        }

        Status = OrderStatus.Paid;
    }

    /// <summary>
    /// Placed -> Cancelled. Returns false when the order was already cancelled (idempotent),
    /// so the caller releases the reserved stock exactly once.
    /// </summary>
    public bool Cancel()
    {
        if (Status == OrderStatus.Cancelled)
        {
            return false;
        }

        if (Status != OrderStatus.Placed)
        {
            throw new DomainException(DomainErrorCodes.OrderNotCancellable, $"Order {OrderNumber} is {Status} and cannot be cancelled.");
        }

        Status = OrderStatus.Cancelled;
        return true;
    }
}
