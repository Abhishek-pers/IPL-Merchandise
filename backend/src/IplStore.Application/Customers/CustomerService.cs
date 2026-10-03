namespace IplStore.Application.Customers;

/// <summary>
/// Lists shoppers for the demo "who am I" picker. In production this is replaced by an
/// identity provider (see docs/07-deployment-roadmap.md) and this service disappears.
/// </summary>
public interface ICustomerService
{
    Task<IReadOnlyList<CustomerDto>> ListAsync(CancellationToken cancellationToken);
}

/// <inheritdoc />
public sealed class CustomerService : ICustomerService
{
    private readonly ICustomerRepository _customers;

    public CustomerService(ICustomerRepository customers)
    {
        _customers = customers;
    }

    public async Task<IReadOnlyList<CustomerDto>> ListAsync(CancellationToken cancellationToken)
    {
        var customers = await _customers.ListAsync(cancellationToken);
        return customers.Select(c => new CustomerDto(c.Id, c.FullName, c.Email)).ToList();
    }
}
