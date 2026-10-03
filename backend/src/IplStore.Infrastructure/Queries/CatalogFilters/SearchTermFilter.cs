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
