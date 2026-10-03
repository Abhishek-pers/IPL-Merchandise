using System.Text.Json;
using IplStore.Application.Catalog;
using IplStore.Application.Common;
using IplStore.Infrastructure.Persistence;
using IplStore.Infrastructure.Persistence.ReadModels;
using IplStore.Infrastructure.Queries.CatalogFilters;
using Microsoft.EntityFrameworkCore;

namespace IplStore.Infrastructure.Queries;

/// <summary>
/// Catalogue reads against the denormalised <c>product_catalog</c> table through the
/// read-only (replica-capable) context. No joins on the hot path.
/// </summary>
internal sealed class CatalogQueries : ICatalogQueries
{
    private readonly ReadOnlyStoreDbContext _db;
    private readonly IEnumerable<ICatalogFilter> _filters;

    public CatalogQueries(ReadOnlyStoreDbContext db, IEnumerable<ICatalogFilter> filters)
    {
        _db = db;
        _filters = filters;
    }

    public async Task<PagedResult<ProductSummaryDto>> SearchAsync(
        ProductSearchCriteria criteria,
        PageRequest page,
        CancellationToken cancellationToken)
    {
        IQueryable<CatalogItem> query = _db.Catalog.Where(c => c.IsActive);
        foreach (var filter in _filters)
        {
            query = filter.Apply(query, criteria);
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await CatalogSorting.Apply(query, criteria.Sort)
            .Skip(page.Offset)
            .Take(page.PageSize)
            .Select(c => new ProductSummaryDto(
                c.ProductId,
                c.Sku,
                c.Name,
                c.Price,
                c.Currency,
                c.StockQuantity > 0,
                c.StockQuantity,
                c.FranchiseCode,
                c.FranchiseName,
                c.FranchiseColor,
                c.CategoryCode,
                c.CategoryName,
                c.ImageUrl))
            .ToListAsync(cancellationToken);

        return new PagedResult<ProductSummaryDto>(items, page.Page, page.PageSize, totalCount);
    }

    public async Task<ProductDetailsDto?> GetDetailsAsync(Guid productId, CancellationToken cancellationToken)
    {
        var c = await _db.Catalog.FirstOrDefaultAsync(x => x.ProductId == productId && x.IsActive, cancellationToken);
        if (c is null)
        {
            return null;
        }

        return new ProductDetailsDto(
            c.ProductId,
            c.Sku,
            c.Name,
            c.Description,
            c.Price,
            c.Currency,
            c.StockQuantity,
            c.StockQuantity > 0,
            c.FranchiseCode,
            c.FranchiseName,
            c.FranchiseColor,
            c.CategoryCode,
            c.CategoryName,
            c.ImageUrl,
            ParseAttributes(c.Attributes));
    }

    public async Task<IReadOnlyList<FranchiseDto>> ListFranchisesAsync(CancellationToken cancellationToken) =>
        await _db.Franchises
            .OrderBy(f => f.Name)
            .Select(f => new FranchiseDto(f.Id, f.Code, f.Name, f.City, f.PrimaryColor))
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<CategoryDto>> ListCategoriesAsync(CancellationToken cancellationToken) =>
        await _db.Categories
            .OrderBy(c => c.Name)
            .Select(c => new CategoryDto(c.Id, c.Code, c.Name))
            .ToListAsync(cancellationToken);

    private static IReadOnlyDictionary<string, JsonElement> ParseAttributes(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return new Dictionary<string, JsonElement>();
        }

        return JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json)
               ?? new Dictionary<string, JsonElement>();
    }
}
