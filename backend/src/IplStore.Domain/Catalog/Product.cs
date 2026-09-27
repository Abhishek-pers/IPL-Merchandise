using IplStore.Domain.Common;

namespace IplStore.Domain.Catalog;

/// <summary>
/// A sellable item (write model). Reads for the storefront come from the denormalised
/// <c>product_catalog</c> projection instead; this entity is used when we must be
/// transactionally correct, i.e. at checkout.
/// </summary>
/// <remarks>
/// Stock is NOT decremented through this entity. Reservation is a single atomic
/// conditional UPDATE in the repository (<c>... WHERE stock_quantity &gt;= @qty</c>) because a
/// read-modify-write in memory would race between concurrent checkouts.
/// </remarks>
public sealed class Product
{
    private Product()
    {
        // Required by the ORM.
    }

    public Product(
        Guid id,
        string sku,
        string name,
        string description,
        Guid franchiseId,
        Guid categoryId,
        decimal price,
        string currency,
        int stockQuantity,
        DateTimeOffset createdAt)
    {
        Id = Guard.NotEmpty(id, nameof(id));
        Sku = Guard.NotBlank(sku, nameof(sku), 40);
        Name = Guard.NotBlank(name, nameof(name), 200);
        Description = description ?? string.Empty;
        FranchiseId = Guard.NotEmpty(franchiseId, nameof(franchiseId));
        CategoryId = Guard.NotEmpty(categoryId, nameof(categoryId));
        Price = Guard.NotNegative(price, nameof(price));
        Currency = Guard.NotBlank(currency, nameof(currency), 3).ToUpperInvariant();
        StockQuantity = Guard.NotNegative(stockQuantity, nameof(stockQuantity));
        IsActive = true;
        CreatedAt = createdAt;
        UpdatedAt = createdAt;
    }

    public Guid Id { get; private set; }

    public string Sku { get; private set; } = string.Empty;

    public string Name { get; private set; } = string.Empty;

    public string Description { get; private set; } = string.Empty;

    public Guid FranchiseId { get; private set; }

    public Franchise? Franchise { get; private set; }

    public Guid CategoryId { get; private set; }

    public ProductCategory? Category { get; private set; }

    public decimal Price { get; private set; }

    public string Currency { get; private set; } = "INR";

    public int StockQuantity { get; private set; }

    public string? ImageUrl { get; private set; }

    public bool IsActive { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    /// <summary>
    /// Optimistic-concurrency token (PostgreSQL <c>xmin</c>). Any tracked update of a product
    /// (e.g. a future admin price change) fails with a conflict instead of silently
    /// overwriting a concurrent change ("lost update").
    /// </summary>
    public uint Version { get; private set; }

    /// <summary>Soft check used for fast feedback when adding to cart (not a reservation).</summary>
    public bool CanFulfil(int quantity) => IsActive && quantity > 0 && StockQuantity >= quantity;

    public void Deactivate() => IsActive = false;

    /// <summary>
    /// Sets navigation properties for in-memory scenarios (unit tests). The ORM populates
    /// them through <c>Include</c> in production code.
    /// </summary>
    internal void AttachReferences(Franchise franchise, ProductCategory category)
    {
        Franchise = franchise;
        FranchiseId = franchise.Id;
        Category = category;
        CategoryId = category.Id;
    }
}
