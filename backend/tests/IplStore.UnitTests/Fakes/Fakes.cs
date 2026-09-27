using IplStore.Application.Carts;
using IplStore.Application.Catalog;
using IplStore.Application.Common;
using IplStore.Application.Customers;
using IplStore.Application.Orders;
using IplStore.Domain.Carts;
using IplStore.Domain.Catalog;
using IplStore.Domain.Customers;
using IplStore.Domain.Orders;
using Microsoft.Extensions.Options;

namespace IplStore.UnitTests.Fakes;

/// <summary>Deterministic clock.</summary>
public sealed class FixedTimeProvider : TimeProvider
{
    public FixedTimeProvider(DateTimeOffset now)
    {
        Now = now;
    }

    public DateTimeOffset Now { get; set; }

    public override DateTimeOffset GetUtcNow() => Now;
}

/// <summary>IOptionsMonitor whose value a test can change mid-test (simulates a config reload).</summary>
public sealed class TestOptionsMonitor<T> : IOptionsMonitor<T>
{
    public TestOptionsMonitor(T value)
    {
        CurrentValue = value;
    }

    public T CurrentValue { get; set; }

    public T Get(string? name) => CurrentValue;

    public IDisposable? OnChange(Action<T, string?> listener) => null;
}

public sealed class FakeUnitOfWork : IUnitOfWork
{
    private readonly InMemoryStore _store;

    public FakeUnitOfWork(InMemoryStore store)
    {
        _store = store;
    }

    public int Executions { get; private set; }

    public async Task<TResult> ExecuteInTransactionAsync<TResult>(Func<CancellationToken, Task<TResult>> work, CancellationToken cancellationToken)
    {
        Executions++;
        var snapshot = _store.SnapshotStock();
        try
        {
            var result = await work(cancellationToken);
            _store.Commit();
            return result;
        }
        catch
        {
            _store.Rollback(snapshot);
            throw;
        }
    }
}

public sealed class FakeCartRepository : ICartRepository
{
    private static readonly CartPolicy Unbounded = new(int.MaxValue, int.MaxValue);
    private readonly InMemoryStore _store;
    private readonly TimeProvider _time;

    public FakeCartRepository(InMemoryStore store, TimeProvider time)
    {
        _store = store;
        _time = time;
    }

    public Task<Cart> GetOrCreateForUpdateAsync(Guid customerId, CancellationToken cancellationToken)
    {
        // Rebuild a fresh aggregate from committed state, like a reload from the database.
        var now = _time.GetUtcNow();
        var cartId = _store.Carts.TryGetValue(customerId, out var existing) ? existing.CartId : Guid.NewGuid();
        var cart = new Cart(cartId, customerId, now);
        foreach (var (productId, quantity) in _store.CartLines(customerId))
        {
            cart.AddItem(productId, quantity, Unbounded, now);
        }

        _store.TrackedCarts.Add(cart);
        return Task.FromResult(cart);
    }
}

public sealed class FakeCartQueries : ICartQueries
{
    private readonly InMemoryStore _store;

    public FakeCartQueries(InMemoryStore store)
    {
        _store = store;
    }

    public Task<CartView?> GetAsync(Guid customerId, CancellationToken cancellationToken)
    {
        if (!_store.Carts.TryGetValue(customerId, out var cart))
        {
            return Task.FromResult<CartView?>(null);
        }

        var lines = cart.Lines.Select(l =>
        {
            var p = _store.Products[l.ProductId];
            return new CartLineView(p.Id, p.Sku, p.Name, p.Franchise?.Code ?? "", p.Franchise?.Name ?? "", p.Category?.Name ?? "", p.Price, l.Quantity, p.IsActive, _store.Stock[p.Id]);
        }).ToList();

        return Task.FromResult<CartView?>(new CartView(cart.CartId, DateTimeOffset.UnixEpoch, lines));
    }
}

public sealed class FakeProductRepository : IProductRepository
{
    private readonly InMemoryStore _store;

    public FakeProductRepository(InMemoryStore store)
    {
        _store = store;
    }

    public Task<Product?> FindAsync(Guid productId, CancellationToken cancellationToken) =>
        Task.FromResult(_store.Products.GetValueOrDefault(productId));

    public Task<IReadOnlyList<Product>> GetWithReferencesAsync(IReadOnlyCollection<Guid> productIds, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<Product>>(productIds.Where(_store.Products.ContainsKey).Select(id => _store.Products[id]).ToList());

    public Task<bool> TryReserveStockAsync(Guid productId, int quantity, CancellationToken cancellationToken) =>
        Task.FromResult(_store.TryReserve(productId, quantity));
}

public sealed class FakeOrderRepository : IOrderRepository
{
    private readonly InMemoryStore _store;

    public FakeOrderRepository(InMemoryStore store)
    {
        _store = store;
    }

    public Task<Order?> FindByIdempotencyKeyAsync(Guid customerId, string idempotencyKey, CancellationToken cancellationToken) =>
        Task.FromResult(_store.Orders.FirstOrDefault(o => o.CustomerId == customerId && o.IdempotencyKey == idempotencyKey));

    public void Add(Order order) => _store.PendingOrders.Add(order);
}

public sealed class FakeOrderQueries : IOrderQueries
{
    private readonly InMemoryStore _store;

    public FakeOrderQueries(InMemoryStore store)
    {
        _store = store;
    }

    public Task<PagedResult<OrderSummaryDto>> ListAsync(Guid customerId, PageRequest page, CancellationToken cancellationToken)
    {
        var all = _store.Orders.Where(o => o.CustomerId == customerId).OrderByDescending(o => o.PlacedAt).ToList();
        var items = all.Skip(page.Offset).Take(page.PageSize)
            .Select(o => new OrderSummaryDto(o.Id, o.OrderNumber, o.Status, o.PlacedAt, o.ItemCount, o.Total, o.Currency))
            .ToList();
        return Task.FromResult(new PagedResult<OrderSummaryDto>(items, page.Page, page.PageSize, all.Count));
    }

    public Task<OrderDetailsDto?> GetAsync(Guid customerId, Guid orderId, CancellationToken cancellationToken)
    {
        var o = _store.Orders.FirstOrDefault(x => x.Id == orderId && x.CustomerId == customerId);
        if (o is null)
        {
            return Task.FromResult<OrderDetailsDto?>(null);
        }

        var lines = o.Items
            .Select(i => new OrderLineDto(i.ProductId, i.Sku, i.ProductName, i.FranchiseName, i.CategoryName, i.UnitPrice, i.Quantity, i.LineTotal))
            .ToList();
        return Task.FromResult<OrderDetailsDto?>(new OrderDetailsDto(
            o.Id, o.OrderNumber, o.Status, o.PlacedAt, o.ItemCount,
            new PriceSummaryDto(o.Subtotal, o.Tax, o.Shipping, o.Total, o.Currency), lines));
    }
}

public sealed class FakeCustomerRepository : ICustomerRepository
{
    private readonly InMemoryStore _store;

    public FakeCustomerRepository(InMemoryStore store)
    {
        _store = store;
    }

    public Task<Customer?> FindAsync(Guid customerId, CancellationToken cancellationToken) =>
        Task.FromResult(_store.Customers.GetValueOrDefault(customerId));

    public Task<IReadOnlyList<Customer>> ListAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<Customer>>(_store.Customers.Values.ToList());
}

public sealed class SequentialOrderNumberGenerator : IOrderNumberGenerator
{
    private int _next;

    public string Next(DateTimeOffset placedAt) => $"TEST-{Interlocked.Increment(ref _next):D6}";
}
