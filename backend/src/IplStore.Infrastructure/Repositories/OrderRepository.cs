using IplStore.Application.Orders;
using IplStore.Domain.Orders;
using IplStore.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace IplStore.Infrastructure.Repositories;

internal sealed class OrderRepository : IOrderRepository
{
    private readonly StoreDbContext _db;

    public OrderRepository(StoreDbContext db)
    {
        _db = db;
    }

    public Task<Order?> FindByIdempotencyKeyAsync(Guid customerId, string idempotencyKey, CancellationToken cancellationToken) =>
        _db.Orders
            .AsNoTracking()
            .FirstOrDefaultAsync(o => o.CustomerId == customerId && o.IdempotencyKey == idempotencyKey, cancellationToken);

    public void Add(Order order) => _db.Orders.Add(order);

    public async Task<Order?> FindForUpdateAsync(Guid customerId, Guid orderId, CancellationToken cancellationToken)
    {
        if (_db.Database.CurrentTransaction is null)
        {
            throw new InvalidOperationException(
                $"{nameof(FindForUpdateAsync)} must run inside IUnitOfWork.ExecuteInTransactionAsync; a row lock without a transaction is released immediately.");
        }

        // customer_id in the WHERE clause: another customer's order id behaves as "not found".
        await _db.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT 1 FROM orders WHERE id = {orderId} AND customer_id = {customerId} FOR UPDATE",
            cancellationToken);

        return await _db.Orders
            .Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.Id == orderId && o.CustomerId == customerId, cancellationToken);
    }
}
