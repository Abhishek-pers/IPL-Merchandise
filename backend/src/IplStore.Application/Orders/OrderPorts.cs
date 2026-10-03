using IplStore.Application.Common;
using IplStore.Domain.Orders;

namespace IplStore.Application.Orders;

/// <summary>Write-side persistence port for orders.</summary>
public interface IOrderRepository
{
    Task<Order?> FindByIdempotencyKeyAsync(Guid customerId, string idempotencyKey, CancellationToken cancellationToken);

    /// <summary>Stages a new order; persisted when the unit of work commits.</summary>
    void Add(Order order);

    /// <summary>
    /// Loads the customer's order (with lines) under a row lock, so a concurrent pay and cancel
    /// of the same order are serialised. Must run inside a unit-of-work transaction.
    /// </summary>
    Task<Order?> FindForUpdateAsync(Guid customerId, Guid orderId, CancellationToken cancellationToken);
}

/// <summary>Charge request sent to the payment provider.</summary>
/// <param name="SimulateFailure">Test-mode switch for the dummy gateway (a real provider would use test card numbers).</param>
public sealed record PaymentRequest(Guid OrderId, string OrderNumber, decimal Amount, string Currency, bool SimulateFailure);

public sealed record PaymentResult(bool Succeeded, string? TransactionId, string? FailureReason)
{
    public static PaymentResult Success(string transactionId) => new(true, transactionId, null);

    public static PaymentResult Declined(string reason) => new(false, null, reason);
}

/// <summary>
/// Port to the payment provider. Implementations must treat <see cref="PaymentRequest.OrderId"/>
/// as the idempotency key, so charging the same order twice never takes money twice.
/// </summary>
public interface IPaymentGateway
{
    Task<PaymentResult> ChargeAsync(PaymentRequest request, CancellationToken cancellationToken);
}

/// <summary>
/// Read-side port for order history. Reads the PRIMARY (read-your-writes): a customer who
/// just checked out must see the order immediately, so replica lag is not acceptable here.
/// </summary>
public interface IOrderQueries
{
    Task<PagedResult<OrderSummaryDto>> ListAsync(Guid customerId, PageRequest page, CancellationToken cancellationToken);

    Task<OrderDetailsDto?> GetAsync(Guid customerId, Guid orderId, CancellationToken cancellationToken);
}

/// <summary>Strategy for human-friendly order numbers (e.g. IPL-20260924-7KQ2M9XA).</summary>
public interface IOrderNumberGenerator
{
    string Next(DateTimeOffset placedAt);
}
