using IplStore.Api.Contracts;
using IplStore.Application.Catalog;
using IplStore.Application.Common;
using IplStore.Application.Customers;
using Microsoft.AspNetCore.Mvc;

namespace IplStore.Api.Controllers;

/// <summary>Product list, search and details (requirements 1, 2 and 3).</summary>
[ApiController]
[Route(ApiRoutes.V1 + "/products")]
[Produces("application/json")]
public sealed class ProductsController : ControllerBase
{
    private readonly ICatalogService _catalog;

    public ProductsController(ICatalogService catalog)
    {
        _catalog = catalog;
    }

    /// <summary>Lists / searches products by name, type, franchise and price with paging and sorting.</summary>
    [HttpGet]
    [ProducesResponseType<PagedResult<ProductSummaryDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public Task<PagedResult<ProductSummaryDto>> Search([FromQuery] SearchProductsRequest request, CancellationToken cancellationToken) =>
        _catalog.SearchAsync(request.ToQuery(), cancellationToken);

    /// <summary>Full details of one product.</summary>
    [HttpGet("{productId:guid}")]
    [ProducesResponseType<ProductDetailsDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public Task<ProductDetailsDto> Get(Guid productId, CancellationToken cancellationToken) =>
        _catalog.GetProductAsync(productId, cancellationToken);
}

/// <summary>Reference data used to build search filters.</summary>
[ApiController]
[Route(ApiRoutes.V1)]
[Produces("application/json")]
public sealed class ReferenceDataController : ControllerBase
{
    private readonly ICatalogService _catalog;
    private readonly ICustomerService _customers;

    public ReferenceDataController(ICatalogService catalog, ICustomerService customers)
    {
        _catalog = catalog;
        _customers = customers;
    }

    [HttpGet("franchises")]
    [ResponseCache(Duration = 300)]
    public Task<IReadOnlyList<FranchiseDto>> Franchises(CancellationToken cancellationToken) =>
        _catalog.ListFranchisesAsync(cancellationToken);

    [HttpGet("categories")]
    [ResponseCache(Duration = 300)]
    public Task<IReadOnlyList<CategoryDto>> Categories(CancellationToken cancellationToken) =>
        _catalog.ListCategoriesAsync(cancellationToken);

    /// <summary>Demo shoppers for the UI's customer picker (stand-in for real authentication).</summary>
    [HttpGet("customers")]
    public Task<IReadOnlyList<CustomerDto>> Customers(CancellationToken cancellationToken) =>
        _customers.ListAsync(cancellationToken);
}
