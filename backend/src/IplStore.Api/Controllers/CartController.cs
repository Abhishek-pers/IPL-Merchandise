using IplStore.Api.Contracts;
using IplStore.Api.Identity;
using IplStore.Application.Carts;
using Microsoft.AspNetCore.Mvc;

namespace IplStore.Api.Controllers;

/// <summary>The caller's shopping cart (requirement 4). Every mutation returns the full cart.</summary>
[ApiController]
[RequiresCustomer]
[Route(ApiRoutes.V1 + "/cart")]
[Produces("application/json")]
public sealed class CartController : ControllerBase
{
    private readonly ICartService _carts;
    private readonly ICurrentCustomerAccessor _currentCustomer;

    public CartController(ICartService carts, ICurrentCustomerAccessor currentCustomer)
    {
        _carts = carts;
        _currentCustomer = currentCustomer;
    }

    [HttpGet]
    [ProducesResponseType<CartDto>(StatusCodes.Status200OK)]
    public Task<CartDto> Get(CancellationToken cancellationToken) =>
        _carts.GetAsync(_currentCustomer.GetRequiredCustomerId(), cancellationToken);

    /// <summary>
    /// Adds units of a product. Adding a product already in the cart increases its quantity.
    /// Send a fresh UUID in <c>Idempotency-Key</c> per click and REUSE it when retrying: a repeat
    /// with the same key returns the cart without adding again.
    /// </summary>
    [HttpPost("items")]
    [ProducesResponseType<CartDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public Task<CartDto> AddItem(
        [FromBody] AddCartItemRequest request,
        [FromHeader(Name = OrdersController.IdempotencyKeyHeader)] string? idempotencyKey,
        CancellationToken cancellationToken) =>
        _carts.AddItemAsync(
            new AddCartItemCommand(_currentCustomer.GetRequiredCustomerId(), request.ProductId, request.Quantity, idempotencyKey),
            cancellationToken);

    /// <summary>Sets the quantity of a line (0 removes it). PUT is idempotent: safe to retry.</summary>
    [HttpPut("items/{productId:guid}")]
    [ProducesResponseType<CartDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public Task<CartDto> UpdateItem(Guid productId, [FromBody] UpdateCartItemRequest request, CancellationToken cancellationToken) =>
        _carts.UpdateItemAsync(
            new UpdateCartItemCommand(_currentCustomer.GetRequiredCustomerId(), productId, request.Quantity),
            cancellationToken);

    [HttpDelete("items/{productId:guid}")]
    [ProducesResponseType<CartDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public Task<CartDto> RemoveItem(Guid productId, CancellationToken cancellationToken) =>
        _carts.RemoveItemAsync(
            new RemoveCartItemCommand(_currentCustomer.GetRequiredCustomerId(), productId),
            cancellationToken);
}
