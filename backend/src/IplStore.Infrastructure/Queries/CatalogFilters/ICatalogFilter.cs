using IplStore.Application.Catalog;
using IplStore.Infrastructure.Persistence.ReadModels;

namespace IplStore.Infrastructure.Queries.CatalogFilters;

/// <summary>
/// One composable search filter (Specification / Pipeline pattern).
/// <para>
/// To add a new filter (e.g. "size = XL"): add a property to <see cref="SearchProductsQuery"/>
/// and <see cref="ProductSearchCriteria"/>, create a class implementing this interface and
/// register it in <c>Infrastructure/DependencyInjection.cs</c>. No existing filter or the query
/// class changes (Open/Closed principle).
/// </para>
/// </summary>
public interface ICatalogFilter
{
    IQueryable<CatalogItem> Apply(IQueryable<CatalogItem> query, ProductSearchCriteria criteria);
}
