using IplStore.Domain.Common;

namespace IplStore.Domain.Carts;

/// <summary>
/// Shopping cart aggregate root. Owns every rule about cart lines:
/// <list type="bullet">
///   <item>adding a product that is already in the cart increments its quantity (no duplicate lines);</item>
///   <item>per-line and per-cart limits come from <see cref="CartPolicy"/>;</item>
///   <item>setting a quantity to 0 removes the line.</item>
/// </list>
/// Concurrency: the repository loads the cart under a row lock (<c>SELECT ... FOR UPDATE</c>)
/// so two requests for the same customer are serialised; a UNIQUE (cart_id, product_id)
/// constraint is the database backstop.
/// </summary>
public sealed class Cart
{
    private readonly List<CartItem> _items = new();

    private Cart()
    {
        // Required by the ORM.
    }

    public Cart(Guid id, Guid customerId, DateTimeOffset createdAt)
    {
        Id = Guard.NotEmpty(id, nameof(id));
        CustomerId = Guard.NotEmpty(customerId, nameof(customerId));
        CreatedAt = createdAt;
        UpdatedAt = createdAt;
    }

    public Guid Id { get; private set; }

    public Guid CustomerId { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public IReadOnlyCollection<CartItem> Items => _items.AsReadOnly();

    public bool IsEmpty => _items.Count == 0;

    public int TotalQuantity => _items.Sum(i => i.Quantity);

    public CartItem? FindItem(Guid productId) => _items.Find(i => i.ProductId == productId);

    /// <summary>Adds <paramref name="quantity"/> units; merges into the existing line if present.</summary>
    public CartItem AddItem(Guid productId, int quantity, CartPolicy policy, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(policy);
        Guard.Positive(quantity, nameof(quantity));

        var existing = FindItem(productId);
        if (existing is not null)
        {
            var newQuantity = existing.Quantity + quantity;
            EnsureWithinLineLimit(newQuantity, policy);
            existing.ChangeQuantity(newQuantity);
            Touch(now);
            return existing;
        }

        if (_items.Count >= policy.MaxDistinctLines)
        {
            throw new DomainException(
                DomainErrorCodes.CartLineLimitReached,
                $"A cart can contain at most {policy.MaxDistinctLines} different products.");
        }

        EnsureWithinLineLimit(quantity, policy);
        var item = new CartItem(Guid.NewGuid(), Id, CustomerId, productId, quantity, now);
        _items.Add(item);
        Touch(now);
        return item;
    }

    /// <summary>Sets an absolute quantity. 0 removes the line.</summary>
    public void SetItemQuantity(Guid productId, int quantity, CartPolicy policy, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(policy);
        Guard.NotNegative(quantity, nameof(quantity));

        if (quantity == 0)
        {
            RemoveItem(productId, now);
            return;
        }

        var item = FindItem(productId) ?? throw ItemNotFound(productId);
        EnsureWithinLineLimit(quantity, policy);
        item.ChangeQuantity(quantity);
        Touch(now);
    }

    public void RemoveItem(Guid productId, DateTimeOffset now)
    {
        var item = FindItem(productId) ?? throw ItemNotFound(productId);
        _items.Remove(item);
        Touch(now);
    }

    /// <summary>Empties the cart (called after a successful checkout).</summary>
    public void Clear(DateTimeOffset now)
    {
        _items.Clear();
        Touch(now);
    }

    private static void EnsureWithinLineLimit(int quantity, CartPolicy policy)
    {
        if (quantity > policy.MaxQuantityPerLine)
        {
            throw new DomainException(
                DomainErrorCodes.CartQuantityLimitExceeded,
                $"You can buy at most {policy.MaxQuantityPerLine} units of a product per order.");
        }
    }

    private static DomainException ItemNotFound(Guid productId) =>
        new(DomainErrorCodes.CartItemNotFound, $"Product '{productId}' is not in the cart.");

    private void Touch(DateTimeOffset now) => UpdatedAt = now;
}
