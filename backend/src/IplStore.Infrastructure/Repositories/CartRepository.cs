using IplStore.Application.Carts;
using IplStore.Domain.Carts;
using IplStore.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace IplStore.Infrastructure.Repositories;

internal sealed class CartRepository : ICartRepository
{
    private readonly StoreDbContext _db;
    private readonly TimeProvider _time;

    public CartRepository(StoreDbContext db, TimeProvider time)
    {
        _db = db;
        _time = time;
    }

    /// <summary>
    /// Race-free get-or-create + lock, in two statements:
    /// <list type="number">
    ///   <item><c>INSERT ... ON CONFLICT (customer_id) DO NOTHING</c> - if two first-ever requests
    ///         arrive together, exactly one insert wins; the other is a no-op (no exception,
    ///         no duplicate cart - guaranteed by uq_carts_customer).</item>
    ///   <item><c>SELECT ... FOR UPDATE</c> - row lock held until commit/rollback, so every other
    ///         transaction touching this customer's cart waits its turn.</item>
    /// </list>
    /// </summary>
    public async Task<Cart> GetOrCreateForUpdateAsync(Guid customerId, CancellationToken cancellationToken)
    {
        if (_db.Database.CurrentTransaction is null)
        {
            throw new InvalidOperationException(
                $"{nameof(GetOrCreateForUpdateAsync)} must run inside IUnitOfWork.ExecuteInTransactionAsync; a row lock without a transaction is released immediately.");
        }

        var now = _time.GetUtcNow();
        var newCartId = Guid.NewGuid();

        await _db.Database.ExecuteSqlInterpolatedAsync(
            $"""
            INSERT INTO carts (id, customer_id, created_at, updated_at)
            VALUES ({newCartId}, {customerId}, {now}, {now})
            ON CONFLICT (customer_id) DO NOTHING
            """,
            cancellationToken);

        await _db.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT 1 FROM carts WHERE customer_id = {customerId} FOR UPDATE",
            cancellationToken);

        return await _db.Carts
            .Include(c => c.Items)
            .SingleAsync(c => c.CustomerId == customerId, cancellationToken);
    }
}
