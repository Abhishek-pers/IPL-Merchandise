using IplStore.Application.Catalog;
using IplStore.Infrastructure.Persistence.ReadModels;
using Microsoft.EntityFrameworkCore;

namespace IplStore.Infrastructure.Queries.CatalogFilters;

/// <summary>
/// Free-text search: EVERY term must appear somewhere in name / SKU / franchise / category.
/// "mumbai jersey" -> search_text ILIKE '%mumbai%' AND search_text ILIKE '%jersey%'.
/// Served by the GIN trigram index ix_product_catalog_search_trgm.
/// </summary>
internal sealed class SearchTermFilter : ICatalogFilter
{
    private const string EscapeCharacter = "\\";

    public IQueryable<CatalogItem> Apply(IQueryable<CatalogItem> query, ProductSearchCriteria criteria)
    {
        foreach (var term in criteria.SearchTerms)
        {
            var pattern = $"%{EscapeLikeWildcards(term)}%";
            query = query.Where(c => EF.Functions.ILike(c.SearchText, pattern, EscapeCharacter));
        }

        return query;
    }

    /// <summary>User input "50%" must match a literal percent sign, not act as a wildcard.</summary>
    internal static string EscapeLikeWildcards(string value) =>
        value.Replace(EscapeCharacter, EscapeCharacter + EscapeCharacter, StringComparison.Ordinal)
             .Replace("%", EscapeCharacter + "%", StringComparison.Ordinal)
             .Replace("_", EscapeCharacter + "_", StringComparison.Ordinal);
}

/// <summary>Filter by one or more franchise codes (CSK, MI ...).</summary>
internal sealed class FranchiseFilter : ICatalogFilter
{
    public IQueryable<CatalogItem> Apply(IQueryable<CatalogItem> query, ProductSearchCriteria criteria)
    {
        if (criteria.FranchiseCodes.Count == 0)
        {
            return query;
        }

        var codes = criteria.FranchiseCodes.ToList();
        return query.Where(c => codes.Contains(c.FranchiseCode));
    }
}

/// <summary>Filter by one or more product types (JERSEY, CAP ...).</summary>
internal sealed class CategoryFilter : ICatalogFilter
{
    public IQueryable<CatalogItem> Apply(IQueryable<CatalogItem> query, ProductSearchCriteria criteria)
    {
        if (criteria.CategoryCodes.Count == 0)
        {
            return query;
        }

        var codes = criteria.CategoryCodes.ToList();
        return query.Where(c => codes.Contains(c.CategoryCode));
    }
}

internal sealed class PriceRangeFilter : ICatalogFilter
{
    public IQueryable<CatalogItem> Apply(IQueryable<CatalogItem> query, ProductSearchCriteria criteria)
    {
        if (criteria.MinPrice is { } min)
        {
            query = query.Where(c => c.Price >= min);
        }

        if (criteria.MaxPrice is { } max)
        {
            query = query.Where(c => c.Price <= max);
        }

        return query;
    }
}

internal sealed class InStockFilter : ICatalogFilter
{
    public IQueryable<CatalogItem> Apply(IQueryable<CatalogItem> query, ProductSearchCriteria criteria) =>
        criteria.InStockOnly ? query.Where(c => c.StockQuantity > 0) : query;
}
