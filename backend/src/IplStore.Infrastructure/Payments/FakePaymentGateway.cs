using System.Collections.Concurrent;
using IplStore.Application.Orders;
using Microsoft.Extensions.Logging;

namespace IplStore.Infrastructure.Payments;

/// <summary>
/// Dummy payment provider for the demo. Approves unless the request asks for a simulated
/// decline. Like a real provider it is idempotent per order: a second charge for an order
/// that was already approved returns the SAME transaction id instead of charging again.
/// Replace with a real adapter (Razorpay, Stripe ...) in DI - nothing else changes.
/// </summary>
internal sealed class FakePaymentGateway : IPaymentGateway
{
    private readonly ConcurrentDictionary<Guid, string> _approved = new();
    private readonly ILogger<FakePaymentGateway> _logger;

    public FakePaymentGateway(ILogger<FakePaymentGateway> logger)
    {
        _logger = logger;
    }

    public Task<PaymentResult> ChargeAsync(PaymentRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (_approved.TryGetValue(request.OrderId, out var existing))
        {
            return Task.FromResult(PaymentResult.Success(existing));
        }

        if (request.SimulateFailure)
        {
            _logger.LogInformation("Simulated decline for order {OrderNumber}", request.OrderNumber);
            return Task.FromResult(PaymentResult.Declined("Card declined by the bank (simulated)."));
        }

        var transactionId = _approved.GetOrAdd(request.OrderId, _ => $"TXN-{Guid.NewGuid():N}"[..16].ToUpperInvariant());
        _logger.LogInformation(
            "Charged {Amount} {Currency} for order {OrderNumber}: {TransactionId}",
            request.Amount, request.Currency, request.OrderNumber, transactionId);
        return Task.FromResult(PaymentResult.Success(transactionId));
    }
}
