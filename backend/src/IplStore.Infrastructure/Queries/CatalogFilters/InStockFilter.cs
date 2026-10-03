using IplStore.Application.Catalog;
using IplStore.Infrastructure.Persistence.ReadModels;

namespace IplStore.Infrastructure.Queries.CatalogFilters;

internal sealed class InStockFilter : ICatalogFilter
{
    public IQueryable<CatalogItem> Apply(IQueryable<CatalogItem> query, ProductSearchCriteria criteria) =>
        criteria.InStockOnly ? query.Where(c => c.StockQuantity > 0) : query;
}
