using IplStore.Application.Common;
using IplStore.Domain.Orders;

namespace IplStore.Application.Orders;

/// <summary>Write-side persistence port for orders.</summary>
public interface IOrderRepository
{
    Task<Order?> FindByIdempotencyKeyAsync(Guid customerId, string idempotencyKey, CancellationToken cancellationToken);

    /// <summary>Stages a new order; persisted when the unit of work commits.</summary>
    void Add(Order order);
}

/// <summary>
/// Read-side port for order history. Reads the PRIMARY (read-your-writes): a customer who
/// just checked out must see the order immediately, so replica lag is not acceptable here.
/// </summary>
public interface IOrderQueries
{
    Task<PagedResult<OrderSummaryDto>> ListAsync(Guid customerId, PageRequest page, CancellationToken cancellationToken);

    Task<OrderDetailsDto?> GetAsync(Guid customerId, Guid orderId, CancellationToken cancellationToken);
}

/// <summary>Strategy for human-friendly order numbers (e.g. IPL-20260924-7KQ2M9XA).</summary>
public interface IOrderNumberGenerator
{
    string Next(DateTimeOffset placedAt);
}
