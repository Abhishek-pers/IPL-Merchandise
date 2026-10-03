using IplStore.Application.Customers;
using IplStore.Domain.Customers;
using IplStore.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace IplStore.Infrastructure.Repositories;

internal sealed class CustomerRepository : ICustomerRepository
{
    private readonly StoreDbContext _db;

    public CustomerRepository(StoreDbContext db)
    {
        _db = db;
    }

    public Task<Customer?> FindAsync(Guid customerId, CancellationToken cancellationToken) =>
        _db.Customers.AsNoTracking().FirstOrDefaultAsync(c => c.Id == customerId, cancellationToken);

    public async Task<IReadOnlyList<Customer>> ListAsync(CancellationToken cancellationToken) =>
        await _db.Customers.AsNoTracking().OrderBy(c => c.FullName).ToListAsync(cancellationToken);
}
