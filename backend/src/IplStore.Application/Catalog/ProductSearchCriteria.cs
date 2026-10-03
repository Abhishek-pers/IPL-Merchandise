namespace IplStore.Application.Catalog;

/// <summary>
/// Validated, normalised search criteria handed to the query side. Codes are upper-cased,
/// the search text is trimmed and split into terms. Infrastructure can trust it blindly.
/// </summary>
public sealed record ProductSearchCriteria(
    IReadOnlyList<string> SearchTerms,
    IReadOnlyCollection<string> FranchiseCodes,
    IReadOnlyCollection<string> CategoryCodes,
    bool InStockOnly,
    ProductSort Sort);
