using IplStore.Domain.Catalog;

namespace IplStore.Application.Catalog;

/// <summary>Write-side persistence port for products (always hits the PRIMARY database).</summary>
public interface IProductRepository
{
    Task<Product?> FindAsync(Guid productId, CancellationToken cancellationToken);

    /// <summary>Loads products with their franchise and category (for order snapshots).</summary>
    Task<IReadOnlyList<Product>> GetWithReferencesAsync(IReadOnlyCollection<Guid> productIds, CancellationToken cancellationToken);

    /// <summary>
    /// Atomically decrements stock if - and only if - enough is available:
    /// <c>UPDATE products SET stock = stock - @q WHERE id = @id AND stock &gt;= @q</c>.
    /// Returns false when the product is sold out / inactive. Never oversells, even with
    /// thousands of concurrent checkouts, because the check and the write are one statement.
    /// </summary>
    Task<bool> TryReserveStockAsync(Guid productId, int quantity, CancellationToken cancellationToken);

    /// <summary>
    /// Compensation for <see cref="TryReserveStockAsync"/>: puts reserved units back
    /// (<c>UPDATE products SET stock = stock + @q WHERE id = @id</c>), e.g. when an unpaid order is cancelled.
    /// </summary>
    Task ReleaseStockAsync(Guid productId, int quantity, CancellationToken cancellationToken);
}
