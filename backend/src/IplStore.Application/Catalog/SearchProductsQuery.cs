namespace IplStore.Application.Catalog;

/// <summary>
/// Raw search input as received from a client (every field optional).
/// Parameter object: adding a new filter = adding a property, no signature changes.
/// </summary>
public sealed record SearchProductsQuery
{
    /// <summary>Free text matched against name, SKU, franchise and category.</summary>
    public string? Search { get; init; }

    /// <summary>Franchise codes, e.g. ["CSK", "MI"].</summary>
    public IReadOnlyCollection<string> Franchises { get; init; } = Array.Empty<string>();

    /// <summary>Category codes, e.g. ["JERSEY", "CAP"].</summary>
    public IReadOnlyCollection<string> Categories { get; init; } = Array.Empty<string>();

    public bool InStockOnly { get; init; }

    public ProductSort Sort { get; init; } = ProductSort.Name;

    public int? Page { get; init; }

    public int? PageSize { get; init; }
}
