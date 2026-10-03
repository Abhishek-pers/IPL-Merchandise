using IplStore.Application.Common;

namespace IplStore.Application.Orders;

/// <summary>
/// Read-side port for order history. Reads the PRIMARY (read-your-writes): a customer who
/// just checked out must see the order immediately, so replica lag is not acceptable here.
/// </summary>
public interface IOrderQueries
{
    Task<PagedResult<OrderSummaryDto>> ListAsync(Guid customerId, PageRequest page, CancellationToken cancellationToken);

    Task<OrderDetailsDto?> GetAsync(Guid customerId, Guid orderId, CancellationToken cancellationToken);
}
