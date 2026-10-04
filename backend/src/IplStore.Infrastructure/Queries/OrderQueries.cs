using IplStore.Application.Common;
using IplStore.Application.Orders;
using IplStore.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace IplStore.Infrastructure.Queries;

/// <summary>
/// Order history straight from the denormalised orders / order_items tables: no joins to
/// products, franchises or categories are needed (and history survives catalogue changes).
/// </summary>
internal sealed class OrderQueries : IOrderQueries
{
    private readonly StoreDbContext _db;

    public OrderQueries(StoreDbContext db)
    {
        _db = db;
    }

    public async Task<PagedResult<OrderSummaryDto>> ListAsync(Guid customerId, PageRequest page, CancellationToken cancellationToken)
    {
        var query = _db.Orders.AsNoTracking().Where(o => o.CustomerId == customerId);
        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(o => o.PlacedAt)
            .ThenByDescending(o => o.Id)
            .Skip(page.Offset)
            .Take(page.PageSize)
            .Select(OrderSummaryDto.Projection)
            .ToListAsync(cancellationToken);

        return new PagedResult<OrderSummaryDto>(items, page.Page, page.PageSize, totalCount);
    }

    public async Task<OrderDetailsDto?> GetAsync(Guid customerId, Guid orderId, CancellationToken cancellationToken)
    {
        var order = await _db.Orders
            .AsNoTracking()
            .Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.Id == orderId && o.CustomerId == customerId, cancellationToken);

        return order is null ? null : OrderDetailsDto.From(order);
    }
}
