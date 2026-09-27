namespace IplStore.Infrastructure.Persistence.ReadModels;

/// <summary>
/// Row of the denormalised <c>product_catalog</c> read model (database/migrations/V002).
/// Written ONLY by database triggers; the application treats it as read-only.
/// </summary>
public sealed class CatalogItem
{
    public Guid ProductId { get; set; }

    public string Sku { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public decimal Price { get; set; }

    public string Currency { get; set; } = string.Empty;

    public int StockQuantity { get; set; }

    public bool IsActive { get; set; }

    public string? ImageUrl { get; set; }

    /// <summary>Raw jsonb (sizes, material, signed_by ...).</summary>
    public string Attributes { get; set; } = "{}";

    public Guid FranchiseId { get; set; }

    public string FranchiseCode { get; set; } = string.Empty;

    public string FranchiseName { get; set; } = string.Empty;

    public string FranchiseColor { get; set; } = string.Empty;

    public Guid CategoryId { get; set; }

    public string CategoryCode { get; set; } = string.Empty;

    public string CategoryName { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    /// <summary>Generated column: lower-cased name + sku + franchise + category (trigram-indexed).</summary>
    public string SearchText { get; set; } = string.Empty;
}
