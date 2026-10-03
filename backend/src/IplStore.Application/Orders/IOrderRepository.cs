using IplStore.Domain.Orders;

namespace IplStore.Application.Orders;

/// <summary>Write-side persistence port for orders.</summary>
public interface IOrderRepository
{
    Task<Order?> FindByIdempotencyKeyAsync(Guid customerId, string idempotencyKey, CancellationToken cancellationToken);

    /// <summary>Stages a new order; persisted when the unit of work commits.</summary>
    void Add(Order order);

    /// <summary>
    /// Loads the customer's order (with lines) under a row lock, so a concurrent pay and cancel
    /// of the same order are serialised. Must run inside a unit-of-work transaction.
    /// </summary>
    Task<Order?> FindForUpdateAsync(Guid customerId, Guid orderId, CancellationToken cancellationToken);
}
