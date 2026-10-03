namespace IplStore.Application.Carts;

/// <summary>Read-side port: the cart joined with current catalogue prices.</summary>
public interface ICartQueries
{
    Task<CartView?> GetAsync(Guid customerId, CancellationToken cancellationToken);
}
