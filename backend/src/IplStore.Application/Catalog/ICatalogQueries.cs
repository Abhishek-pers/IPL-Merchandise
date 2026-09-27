using IplStore.Application.Common;

namespace IplStore.Application.Catalog;

/// <summary>
/// Read-side port for the catalogue (CQRS-lite). Implemented against the denormalised
/// <c>product_catalog</c> table and may be served by a read replica, so results can lag
/// the primary by a few milliseconds - acceptable for browsing, never used for checkout.
/// </summary>
public interface ICatalogQueries
{
    Task<PagedResult<ProductSummaryDto>> SearchAsync(
        ProductSearchCriteria criteria,
        PageRequest page,
        CancellationToken cancellationToken);

    Task<ProductDetailsDto?> GetDetailsAsync(Guid productId, CancellationToken cancellationToken);

    Task<IReadOnlyList<FranchiseDto>> ListFranchisesAsync(CancellationToken cancellationToken);

    Task<IReadOnlyList<CategoryDto>> ListCategoriesAsync(CancellationToken cancellationToken);
}
