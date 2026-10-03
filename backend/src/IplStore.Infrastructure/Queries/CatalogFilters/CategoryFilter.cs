using IplStore.Application.Catalog;
using IplStore.Infrastructure.Persistence.ReadModels;

namespace IplStore.Infrastructure.Queries.CatalogFilters;

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
