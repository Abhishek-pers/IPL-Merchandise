using IplStore.Application.Catalog;
using IplStore.Infrastructure.Persistence.ReadModels;

namespace IplStore.Infrastructure.Queries.CatalogFilters;

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
