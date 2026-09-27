using IplStore.Domain.Carts;
using IplStore.Domain.Catalog;
using IplStore.Domain.Customers;
using IplStore.Domain.Orders;

namespace IplStore.UnitTests.Fakes;

/// <summary>
/// A tiny in-memory "database" with transaction semantics good enough for use-case tests:
/// changes made inside <see cref="FakeUnitOfWork"/> are committed on success and discarded
/// (stock restored, carts/orders untouched) on failure - exactly like a real rollback.
/// </summary>
public sealed class InMemoryStore
{
    private readonly object _gate = new();

    public Dictionary<Guid, Customer> Customers { get; } = new();

    public Dictionary<Guid, Product> Products { get; } = new();

    /// <summary>Authoritative stock (Product.StockQuantity is only the value at creation).</summary>
    public Dictionary<Guid, int> Stock { get; } = new();

    /// <summary>Committed cart lines per customer.</summary>
    public Dictionary<Guid, (Guid CartId, List<(Guid ProductId, int Quantity)> Lines)> Carts { get; } = new();

    public List<Order> Orders { get; } = new();

    internal List<Order> PendingOrders { get; } = new();

    internal List<Cart> TrackedCarts { get; } = new();

    /// <summary>Committed idempotency keys ("customer|operation|key").</summary>
    public HashSet<string> IdempotencyKeys { get; } = new();

    internal HashSet<string> PendingIdempotencyKeys { get; } = new();

    public void AddCustomer(Customer customer) => Customers[customer.Id] = customer;

    public void AddProduct(Product product)
    {
        Products[product.Id] = product;
        Stock[product.Id] = product.StockQuantity;
    }

    public void PutInCart(Guid customerId, Guid productId, int quantity)
    {
        if (!Carts.TryGetValue(customerId, out var cart))
        {
            cart = (Guid.NewGuid(), new List<(Guid, int)>());
            Carts[customerId] = cart;
        }

        cart.Lines.RemoveAll(l => l.ProductId == productId);
        cart.Lines.Add((productId, quantity));
    }

    public IReadOnlyList<(Guid ProductId, int Quantity)> CartLines(Guid customerId) =>
        Carts.TryGetValue(customerId, out var cart) ? cart.Lines : new List<(Guid, int)>();

    internal bool TryReserve(Guid productId, int quantity)
    {
        lock (_gate)
        {
            if (!Products.TryGetValue(productId, out var product) || !product.IsActive || Stock[productId] < quantity)
            {
                return false;
            }

            Stock[productId] -= quantity;
            return true;
        }
    }

    internal Dictionary<Guid, int> SnapshotStock() => new(Stock);

    internal void Commit()
    {
        foreach (var cart in TrackedCarts)
        {
            Carts[cart.CustomerId] = (cart.Id, cart.Items.Select(i => (i.ProductId, i.Quantity)).ToList());
        }

        Orders.AddRange(PendingOrders);
        IdempotencyKeys.UnionWith(PendingIdempotencyKeys);
        Discard();
    }

    internal void Rollback(Dictionary<Guid, int> stockSnapshot)
    {
        Stock.Clear();
        foreach (var (id, qty) in stockSnapshot)
        {
            Stock[id] = qty;
        }

        Discard();
    }

    private void Discard()
    {
        PendingOrders.Clear();
        TrackedCarts.Clear();
        PendingIdempotencyKeys.Clear();
    }
}
