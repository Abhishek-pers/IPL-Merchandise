using IplStore.Domain.Customers;

namespace IplStore.Application.Customers;

/// <summary>Persistence port for customers.</summary>
public interface ICustomerRepository
{
    Task<Customer?> FindAsync(Guid customerId, CancellationToken cancellationToken);

    Task<IReadOnlyList<Customer>> ListAsync(CancellationToken cancellationToken);
}
