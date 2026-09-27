namespace IplStore.Application.Catalog;

/// <summary>Supported orderings for the product list.</summary>
public enum ProductSort
{
    Name = 0,
    PriceLowToHigh = 1,
    PriceHighToLow = 2,
    Newest = 3,
}

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

    public decimal? MinPrice { get; init; }

    public decimal? MaxPrice { get; init; }

    public bool InStockOnly { get; init; }

    public ProductSort Sort { get; init; } = ProductSort.Name;

    public int? Page { get; init; }

    public int? PageSize { get; init; }
}

/// <summary>
/// Validated, normalised search criteria handed to the query side. Codes are upper-cased,
/// the search text is trimmed and split into terms. Infrastructure can trust it blindly.
/// </summary>
public sealed record ProductSearchCriteria(
    IReadOnlyList<string> SearchTerms,
    IReadOnlyCollection<string> FranchiseCodes,
    IReadOnlyCollection<string> CategoryCodes,
    decimal? MinPrice,
    decimal? MaxPrice,
    bool InStockOnly,
    ProductSort Sort);
