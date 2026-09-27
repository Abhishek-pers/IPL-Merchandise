namespace IplStore.Domain.Orders;

/// <summary>Order lifecycle. Persisted as text (see ck_orders_status) so the DB stays readable.</summary>
public enum OrderStatus
{
    Placed = 0,
    Paid = 1,
    Shipped = 2,
    Delivered = 3,
    Cancelled = 4,
}
