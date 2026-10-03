using IplStore.Application.Catalog;
using IplStore.Application.Common;
using IplStore.Domain.Common;
using IplStore.Domain.Orders;
using Microsoft.Extensions.Logging;

namespace IplStore.Application.Orders;

/// <summary>Pays for or cancels a placed order.</summary>
public interface IPaymentService
{
    Task<OrderDetailsDto> PayAsync(PayOrderCommand command, CancellationToken cancellationToken);

    Task<OrderDetailsDto> CancelAsync(Guid customerId, Guid orderId, CancellationToken cancellationToken);
}

/// <summary>
/// Payment is a second step after checkout (checkout already reserved the stock):
/// <list type="number">
///   <item>Check the order is payable. Already paid -> return it (idempotent, no second charge).</item>
///   <item>Call the gateway OUTSIDE any database transaction, so no row lock is held while
///         waiting on a slow external service.</item>
///   <item>Declined -> the order stays Placed and the customer can retry or cancel.</item>
///   <item>Approved -> mark Paid in a short transaction, under a row lock on the order.</item>
/// </list>
/// Cancelling is the compensating action: the order becomes Cancelled and its stock is released.
/// </summary>
public sealed class PaymentService : IPaymentService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IOrderRepository _orders;
    private readonly IOrderQueries _orderQueries;
    private readonly IProductRepository _products;
    private readonly IPaymentGateway _gateway;
    private readonly ILogger<PaymentService> _logger;

    public PaymentService(
        IUnitOfWork unitOfWork,
        IOrderRepository orders,
        IOrderQueries orderQueries,
        IProductRepository products,
        IPaymentGateway gateway,
        ILogger<PaymentService> logger)
    {
        _unitOfWork = unitOfWork;
        _orders = orders;
        _orderQueries = orderQueries;
        _products = products;
        _gateway = gateway;
        _logger = logger;
    }

    public async Task<OrderDetailsDto> PayAsync(PayOrderCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var order = await GetRequiredAsync(command.CustomerId, command.OrderId, cancellationToken);
        if (order.Status == OrderStatus.Paid)
        {
            return order;
        }

        if (order.Status != OrderStatus.Placed)
        {
            throw new DomainException(DomainErrorCodes.OrderNotPayable, $"Order {order.OrderNumber} is {order.Status} and cannot be paid.");
        }

        var payment = await _gateway.ChargeAsync(
            new PaymentRequest(order.Id, order.OrderNumber, order.Price.Total, order.Price.Currency, command.SimulateFailure),
            cancellationToken);

        if (!payment.Succeeded)
        {
            _logger.LogWarning("Payment declined for order {OrderId}: {Reason}", order.Id, payment.FailureReason);
            throw new DomainException(
                DomainErrorCodes.PaymentDeclined,
                $"Payment was declined: {payment.FailureReason} Your order is still reserved - you can retry or cancel it.");
        }

        try
        {
            await _unitOfWork.ExecuteInTransactionAsync(
                async ct =>
                {
                    var tracked = await _orders.FindForUpdateAsync(command.CustomerId, command.OrderId, ct)
                        ?? throw new EntityNotFoundException("Order", command.OrderId);
                    tracked.MarkPaid();
                },
                cancellationToken);
        }
        catch (DomainException ex) when (ex.Code == DomainErrorCodes.OrderNotPayable)
        {
            // The order was cancelled while the charge was in flight: the money must go back.
            _logger.LogError(
                "Order {OrderId} was charged ({TransactionId}) but is no longer payable - refund required",
                order.Id, payment.TransactionId);
            throw;
        }

        _logger.LogInformation("Order {OrderId} paid, transaction {TransactionId}", order.Id, payment.TransactionId);
        return await GetRequiredAsync(command.CustomerId, command.OrderId, cancellationToken);
    }

    public async Task<OrderDetailsDto> CancelAsync(Guid customerId, Guid orderId, CancellationToken cancellationToken)
    {
        await _unitOfWork.ExecuteInTransactionAsync(
            async ct =>
            {
                var order = await _orders.FindForUpdateAsync(customerId, orderId, ct)
                    ?? throw new EntityNotFoundException("Order", orderId);

                if (!order.Cancel())
                {
                    return;
                }

                // Same ascending order as checkout's reservations -> no deadlock between them.
                foreach (var item in order.Items.OrderBy(i => i.ProductId))
                {
                    await _products.ReleaseStockAsync(item.ProductId, item.Quantity, ct);
                }
            },
            cancellationToken);

        _logger.LogInformation("Order {OrderId} cancelled by customer {CustomerId}", orderId, customerId);
        return await GetRequiredAsync(customerId, orderId, cancellationToken);
    }

    private async Task<OrderDetailsDto> GetRequiredAsync(Guid customerId, Guid orderId, CancellationToken cancellationToken) =>
        await _orderQueries.GetAsync(customerId, orderId, cancellationToken)
        ?? throw new EntityNotFoundException("Order", orderId);
}
