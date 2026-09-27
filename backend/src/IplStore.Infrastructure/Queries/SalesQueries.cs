using IplStore.Application.Carts;
using IplStore.Application.Common;
using IplStore.Application.Orders;
using IplStore.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace IplStore.Infrastructure.Queries;

/// <summary>Cart view = cart lines joined with CURRENT catalogue data. Reads the primary.</summary>
internal sealed class CartQueries : ICartQueries
{
    private readonly StoreDbContext _db;

    public CartQueries(StoreDbContext db)
    {
        _db = db;
    }

    public async Task<CartView?> GetAsync(Guid customerId, CancellationToken cancellationToken)
    {
        var cart = await _db.Carts
            .AsNoTracking()
            .Where(c => c.CustomerId == customerId)
            .Select(c => new { c.Id, c.UpdatedAt })
            .FirstOrDefaultAsync(cancellationToken);

        if (cart is null)
        {
            return null;
        }

        var lines = await (
                from item in _db.CartItems.AsNoTracking()
                join product in _db.Catalog.AsNoTracking() on item.ProductId equals product.ProductId
                where item.CartId == cart.Id
                orderby item.AddedAt, item.ProductId
                select new CartLineView(
                    product.ProductId,
                    product.Sku,
                    product.Name,
                    product.FranchiseCode,
                    product.FranchiseName,
                    product.CategoryName,
                    product.Price,
                    item.Quantity,
                    product.IsActive,
                    product.StockQuantity))
            .ToListAsync(cancellationToken);

        return new CartView(cart.Id, cart.UpdatedAt, lines);
    }
}

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
            .Select(o => new OrderSummaryDto(o.Id, o.OrderNumber, o.Status, o.PlacedAt, o.ItemCount, o.Total, o.Currency))
            .ToListAsync(cancellationToken);

        return new PagedResult<OrderSummaryDto>(items, page.Page, page.PageSize, totalCount);
    }

    public async Task<OrderDetailsDto?> GetAsync(Guid customerId, Guid orderId, CancellationToken cancellationToken)
    {
        var order = await _db.Orders
            .AsNoTracking()
            .Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.Id == orderId && o.CustomerId == customerId, cancellationToken);

        if (order is null)
        {
            return null;
        }

        var lines = order.Items
            .OrderBy(i => i.ProductName, StringComparer.Ordinal)
            .Select(i => new OrderLineDto(i.ProductId, i.Sku, i.ProductName, i.FranchiseName, i.CategoryName, i.UnitPrice, i.Quantity, i.LineTotal))
            .ToList();

        return new OrderDetailsDto(
            order.Id,
            order.OrderNumber,
            order.Status,
            order.PlacedAt,
            order.ItemCount,
            new PriceSummaryDto(order.Subtotal, order.Tax, order.Shipping, order.Total, order.Currency),
            lines);
    }
}
