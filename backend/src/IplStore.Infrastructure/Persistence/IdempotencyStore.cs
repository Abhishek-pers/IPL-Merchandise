using IplStore.Application.Common;
using Microsoft.EntityFrameworkCore;

namespace IplStore.Infrastructure.Persistence;

/// <summary>
/// <c>INSERT ... ON CONFLICT DO NOTHING</c> on the idempotency_keys primary key: the first
/// request inserts one row, any repeat inserts zero. Two concurrent duplicates cannot both
/// win: the second waits for the first transaction and then sees the conflict.
/// </summary>
internal sealed class IdempotencyStore : IIdempotencyStore
{
    private readonly StoreDbContext _db;

    public IdempotencyStore(StoreDbContext db)
    {
        _db = db;
    }

    public async Task<bool> TryRecordAsync(Guid customerId, string operation, string idempotencyKey, CancellationToken cancellationToken)
    {
        if (_db.Database.CurrentTransaction is null)
        {
            throw new InvalidOperationException(
                $"{nameof(TryRecordAsync)} must run inside IUnitOfWork.ExecuteInTransactionAsync so the key commits with the change it guards.");
        }

        var inserted = await _db.Database.ExecuteSqlInterpolatedAsync(
            $"""
            INSERT INTO idempotency_keys (customer_id, operation, idempotency_key)
            VALUES ({customerId}, {operation}, {idempotencyKey})
            ON CONFLICT DO NOTHING
            """,
            cancellationToken);

        return inserted == 1;
    }
}
