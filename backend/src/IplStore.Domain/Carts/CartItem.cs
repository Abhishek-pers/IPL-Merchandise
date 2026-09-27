using IplStore.Domain.Common;

namespace IplStore.Domain.Carts;

/// <summary>One line in a cart. Only the <see cref="Cart"/> aggregate root may change it.</summary>
public sealed class CartItem
{
    private CartItem()
    {
        // Required by the ORM.
    }

    internal CartItem(Guid id, Guid cartId, Guid customerId, Guid productId, int quantity, DateTimeOffset addedAt)
    {
        Id = Guard.NotEmpty(id, nameof(id));
        CartId = cartId;
        CustomerId = customerId;
        ProductId = Guard.NotEmpty(productId, nameof(productId));
        Quantity = Guard.Positive(quantity, nameof(quantity));
        AddedAt = addedAt;
    }

    public Guid Id { get; private set; }

    public Guid CartId { get; private set; }

    /// <summary>Denormalised from the cart: the distribution / shard key (docs/06).</summary>
    public Guid CustomerId { get; private set; }

    public Guid ProductId { get; private set; }

    public int Quantity { get; private set; }

    public DateTimeOffset AddedAt { get; private set; }

    internal void ChangeQuantity(int quantity) => Quantity = Guard.Positive(quantity, nameof(quantity));
}
