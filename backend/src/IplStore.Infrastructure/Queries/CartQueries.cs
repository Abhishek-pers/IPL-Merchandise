using IplStore.Application.Carts;
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
