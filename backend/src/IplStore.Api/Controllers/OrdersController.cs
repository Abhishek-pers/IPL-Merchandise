using IplStore.Api.Contracts;
using IplStore.Api.Identity;
using IplStore.Application.Common;
using IplStore.Application.Orders;
using Microsoft.AspNetCore.Mvc;

namespace IplStore.Api.Controllers;

/// <summary>Checkout and order history (requirement 5).</summary>
[ApiController]
[RequiresCustomer]
[Route(ApiRoutes.V1 + "/orders")]
[Produces("application/json")]
public sealed class OrdersController : ControllerBase
{
    public const string IdempotencyKeyHeader = "Idempotency-Key";
    public const string IdempotentReplayedHeader = "Idempotent-Replayed";

    private readonly ICheckoutService _checkout;
    private readonly IOrderService _orders;
    private readonly ICurrentCustomerAccessor _currentCustomer;

    public OrdersController(ICheckoutService checkout, IOrderService orders, ICurrentCustomerAccessor currentCustomer)
    {
        _checkout = checkout;
        _orders = orders;
        _currentCustomer = currentCustomer;
    }

    /// <summary>
    /// Places an order from the current cart.
    /// Send a fresh UUID in <c>Idempotency-Key</c> per checkout and REUSE it when retrying:
    /// the first call returns 201, any repeat returns 200 with the same order and
    /// <c>Idempotent-Replayed: true</c>.
    /// </summary>
    [HttpPost]
    [ProducesResponseType<OrderDetailsDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<OrderDetailsDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<OrderDetailsDto>> PlaceOrder(
        [FromHeader(Name = IdempotencyKeyHeader)] string? idempotencyKey,
        CancellationToken cancellationToken)
    {
        var result = await _checkout.PlaceOrderAsync(
            new PlaceOrderCommand(_currentCustomer.GetRequiredCustomerId(), idempotencyKey),
            cancellationToken);

        if (result.IsReplay)
        {
            Response.Headers[IdempotentReplayedHeader] = "true";
            return Ok(result.Order);
        }

        return CreatedAtAction(nameof(Get), new { orderId = result.Order.Id }, result.Order);
    }

    /// <summary>The caller's orders, newest first.</summary>
    [HttpGet]
    [ProducesResponseType<PagedResult<OrderSummaryDto>>(StatusCodes.Status200OK)]
    public Task<PagedResult<OrderSummaryDto>> List([FromQuery] PageQuery query, CancellationToken cancellationToken) =>
        _orders.ListAsync(_currentCustomer.GetRequiredCustomerId(), query.Page, query.PageSize, cancellationToken);

    [HttpGet("{orderId:guid}")]
    [ProducesResponseType<OrderDetailsDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public Task<OrderDetailsDto> Get(Guid orderId, CancellationToken cancellationToken) =>
        _orders.GetAsync(_currentCustomer.GetRequiredCustomerId(), orderId, cancellationToken);
}
