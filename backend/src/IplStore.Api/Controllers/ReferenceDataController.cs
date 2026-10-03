using IplStore.Application.Catalog;
using IplStore.Application.Customers;
using Microsoft.AspNetCore.Mvc;

namespace IplStore.Api.Controllers;

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
