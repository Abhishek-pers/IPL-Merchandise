using IplStore.Application.Common;
using IplStore.Domain.Common;
using Microsoft.Extensions.Options;

namespace IplStore.Application.Catalog;

/// <summary>Use cases for browsing the catalogue: list/search, details, filter lookups.</summary>
public interface ICatalogService
{
    Task<PagedResult<ProductSummaryDto>> SearchAsync(SearchProductsQuery query, CancellationToken cancellationToken);

    Task<ProductDetailsDto> GetProductAsync(Guid productId, CancellationToken cancellationToken);

    Task<IReadOnlyList<FranchiseDto>> ListFranchisesAsync(CancellationToken cancellationToken);

    Task<IReadOnlyList<CategoryDto>> ListCategoriesAsync(CancellationToken cancellationToken);
}

/// <inheritdoc />
public sealed class CatalogService : ICatalogService
{
    private static readonly char[] TermSeparators = { ' ', '\t', ',' };

    private readonly ICatalogQueries _queries;
    private readonly IOptionsMonitor<CatalogOptions> _catalogOptions;
    private readonly IOptionsMonitor<PagingOptions> _pagingOptions;

    public CatalogService(
        ICatalogQueries queries,
        IOptionsMonitor<CatalogOptions> catalogOptions,
        IOptionsMonitor<PagingOptions> pagingOptions)
    {
        _queries = queries;
        _catalogOptions = catalogOptions;
        _pagingOptions = pagingOptions;
    }

    public Task<PagedResult<ProductSummaryDto>> SearchAsync(SearchProductsQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var criteria = BuildCriteria(query, _catalogOptions.CurrentValue);
        var page = PageRequest.Create(query.Page, query.PageSize, _pagingOptions.CurrentValue);
        return _queries.SearchAsync(criteria, page, cancellationToken);
    }

    public async Task<ProductDetailsDto> GetProductAsync(Guid productId, CancellationToken cancellationToken) =>
        await _queries.GetDetailsAsync(productId, cancellationToken)
        ?? throw new EntityNotFoundException("Product", productId);

    public Task<IReadOnlyList<FranchiseDto>> ListFranchisesAsync(CancellationToken cancellationToken) =>
        _queries.ListFranchisesAsync(cancellationToken);

    public Task<IReadOnlyList<CategoryDto>> ListCategoriesAsync(CancellationToken cancellationToken) =>
        _queries.ListCategoriesAsync(cancellationToken);

    /// <summary>Validates and normalises untrusted input into criteria the query side can trust.</summary>
    internal static ProductSearchCriteria BuildCriteria(SearchProductsQuery query, CatalogOptions options)
    {
        var errors = new Dictionary<string, string[]>();

        var search = query.Search?.Trim() ?? string.Empty;
        if (search.Length > options.MaxSearchLength)
        {
            errors[nameof(query.Search)] = new[] { $"Search text must be at most {options.MaxSearchLength} characters." };
        }

        var franchises = NormaliseCodes(query.Franchises);
        var categories = NormaliseCodes(query.Categories);
        if (franchises.Count > options.MaxFilterValues)
        {
            errors[nameof(query.Franchises)] = new[] { $"At most {options.MaxFilterValues} franchises can be selected." };
        }

        if (categories.Count > options.MaxFilterValues)
        {
            errors[nameof(query.Categories)] = new[] { $"At most {options.MaxFilterValues} categories can be selected." };
        }

        if (!Enum.IsDefined(query.Sort))
        {
            errors[nameof(query.Sort)] = new[] { $"Unsupported sort '{query.Sort}'." };
        }

        if (errors.Count > 0)
        {
            throw new RequestValidationException(errors);
        }

        var terms = search
            .ToLowerInvariant()
            .Split(TermSeparators, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct(StringComparer.Ordinal)
            .Take(options.MaxSearchTerms)
            .ToList();

        return new ProductSearchCriteria(terms, franchises, categories, query.InStockOnly, query.Sort);
    }

    private static IReadOnlyCollection<string> NormaliseCodes(IEnumerable<string>? codes) =>
        (codes ?? Enumerable.Empty<string>())
            .SelectMany(c => (c ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .Select(c => c.ToUpperInvariant())
            .Distinct(StringComparer.Ordinal)
            .ToList();
}
