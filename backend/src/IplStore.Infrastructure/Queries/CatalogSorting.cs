using IplStore.Application.Catalog;
using IplStore.Infrastructure.Persistence.ReadModels;

namespace IplStore.Infrastructure.Queries;

/// <summary>
/// Sort orders. Every order ends with ProductId so paging is deterministic (no row appears on
/// two pages when names/prices tie). New sort = new enum value + one switch arm.
/// </summary>
internal static class CatalogSorting
{
    public static IQueryable<CatalogItem> Apply(IQueryable<CatalogItem> query, ProductSort sort) => sort switch
    {
        ProductSort.Name => query.OrderBy(c => c.Name).ThenBy(c => c.ProductId),
        ProductSort.PriceLowToHigh => query.OrderBy(c => c.Price).ThenBy(c => c.Name).ThenBy(c => c.ProductId),
        ProductSort.PriceHighToLow => query.OrderByDescending(c => c.Price).ThenBy(c => c.Name).ThenBy(c => c.ProductId),
        ProductSort.Newest => query.OrderByDescending(c => c.CreatedAt).ThenBy(c => c.ProductId),
        _ => throw new ArgumentOutOfRangeException(nameof(sort), sort, "Unsupported sort."),
    };
}
