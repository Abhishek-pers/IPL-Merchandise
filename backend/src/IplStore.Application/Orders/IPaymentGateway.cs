namespace IplStore.Application.Orders;

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
