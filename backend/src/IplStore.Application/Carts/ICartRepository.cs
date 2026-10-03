using IplStore.Domain.Carts;

namespace IplStore.Application.Carts;

/// <summary>Write-side persistence port for the cart aggregate.</summary>
public interface ICartRepository
{
    /// <summary>
    /// Returns the customer's cart, creating it if needed, and LOCKS it for the rest of the
    /// current transaction. Must be called inside <see cref="Common.IUnitOfWork"/>.
    /// Concurrent callers for the same customer queue up here, which is what makes
    /// cart edits and checkout race-free.
    /// </summary>
    Task<Cart> GetOrCreateForUpdateAsync(Guid customerId, CancellationToken cancellationToken);
}
