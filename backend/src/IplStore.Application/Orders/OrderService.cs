using IplStore.Application.Common;
using IplStore.Domain.Common;
using Microsoft.Extensions.Options;

namespace IplStore.Application.Orders;

/// <summary>Order history use cases.</summary>
public interface IOrderService
{
    Task<PagedResult<OrderSummaryDto>> ListAsync(Guid customerId, int? page, int? pageSize, CancellationToken cancellationToken);

    Task<OrderDetailsDto> GetAsync(Guid customerId, Guid orderId, CancellationToken cancellationToken);
}

/// <inheritdoc />
public sealed class OrderService : IOrderService
{
    private readonly IOrderQueries _queries;
    private readonly IOptionsMonitor<PagingOptions> _paging;

    public OrderService(IOrderQueries queries, IOptionsMonitor<PagingOptions> paging)
    {
        _queries = queries;
        _paging = paging;
    }

    public Task<PagedResult<OrderSummaryDto>> ListAsync(Guid customerId, int? page, int? pageSize, CancellationToken cancellationToken) =>
        _queries.ListAsync(customerId, PageRequest.Create(page, pageSize, _paging.CurrentValue), cancellationToken);

    /// <remarks>
    /// Another customer's order id returns 404 (not 403) so order ids cannot be probed.
    /// </remarks>
    public async Task<OrderDetailsDto> GetAsync(Guid customerId, Guid orderId, CancellationToken cancellationToken) =>
        await _queries.GetAsync(customerId, orderId, cancellationToken)
        ?? throw new EntityNotFoundException("Order", orderId);
}
