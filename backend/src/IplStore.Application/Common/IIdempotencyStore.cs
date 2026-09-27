namespace IplStore.Application.Common;

/// <summary>
/// Idempotent Receiver port: remembers which client requests were already applied, so a
/// retried request (lost response, double click, transaction retry) is not applied twice.
/// </summary>
/// <remarks>
/// Must be called inside <see cref="IUnitOfWork.ExecuteInTransactionAsync{TResult}"/>: the key is
/// recorded in the SAME transaction as the change it guards, so both commit or neither does.
/// A failed attempt therefore never "uses up" a key.
/// </remarks>
public interface IIdempotencyStore
{
    /// <summary>Longest key accepted (matches the database column).</summary>
    const int MaxKeyLength = 100;

    /// <summary>
    /// Records <paramref name="idempotencyKey"/> for this customer and operation.
    /// Returns <c>true</c> the first time (apply the change) and <c>false</c> if it was already
    /// recorded (duplicate request: skip the change).
    /// </summary>
    Task<bool> TryRecordAsync(Guid customerId, string operation, string idempotencyKey, CancellationToken cancellationToken);
}

/// <summary>Operation names used as the idempotency-key namespace.</summary>
public static class IdempotentOperations
{
    public const string AddCartItem = "cart.add-item";
}
