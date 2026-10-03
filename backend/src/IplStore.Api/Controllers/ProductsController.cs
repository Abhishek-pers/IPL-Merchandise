using IplStore.Api.Contracts;
using IplStore.Application.Catalog;
using IplStore.Application.Common;
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
