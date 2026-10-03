using IplStore.Application.Catalog;
using IplStore.Domain.Catalog;
using IplStore.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace IplStore.Infrastructure.Repositories;

internal sealed class ProductRepository : IProductRepository
{
    private readonly StoreDbContext _db;

    public ProductRepository(StoreDbContext db)
    {
        _db = db;
    }

    public Task<Product?> FindAsync(Guid productId, CancellationToken cancellationToken) =>
        _db.Products.AsNoTracking().FirstOrDefaultAsync(p => p.Id == productId, cancellationToken);

    public async Task<IReadOnlyList<Product>> GetWithReferencesAsync(
        IReadOnlyCollection<Guid> productIds,
        CancellationToken cancellationToken) =>
        await _db.Products
            .AsNoTracking()
            .Include(p => p.Franchise)
            .Include(p => p.Category)
            .Where(p => productIds.Contains(p.Id))
            .ToListAsync(cancellationToken);

    public async Task<bool> TryReserveStockAsync(Guid productId, int quantity, CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(quantity);

        // Check-and-decrement in ONE statement. PostgreSQL takes a row lock for the UPDATE and
        // re-evaluates the WHERE clause after any concurrent writer commits, so the
        // "stock >= quantity" test can never be based on a stale read.
        var affected = await _db.Products
            .Where(p => p.Id == productId && p.IsActive && p.StockQuantity >= quantity)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(p => p.StockQuantity, p => p.StockQuantity - quantity),
                cancellationToken);

        return affected == 1;
    }

    public async Task ReleaseStockAsync(Guid productId, int quantity, CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(quantity);

        // Relative increment in one statement, so it cannot overwrite a concurrent reservation.
        // No IsActive filter: units reserved before a product was deactivated still go back.
        await _db.Products
            .Where(p => p.Id == productId)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(p => p.StockQuantity, p => p.StockQuantity + quantity),
                cancellationToken);
    }
}
