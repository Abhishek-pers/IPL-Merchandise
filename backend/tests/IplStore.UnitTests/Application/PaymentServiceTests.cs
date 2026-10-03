using IplStore.Application.Orders;
using IplStore.Domain.Common;
using IplStore.Domain.Orders;
using IplStore.UnitTests.Fakes;

namespace IplStore.UnitTests.Application;

public sealed class PaymentServiceTests
{
    private readonly UseCaseFixture _f = new();

    [Fact]
    public async Task Successful_payment_marks_the_order_paid()
    {
        var order = await PlaceOrderAsync();

        var paid = await _f.PaymentService.PayAsync(new PayOrderCommand(_f.Customer.Id, order.Id, SimulateFailure: false), default);

        paid.Status.Should().Be(OrderStatus.Paid);
        _f.PaymentGateway.Charges.Should().ContainSingle().Which.Amount.Should().Be(order.Price.Total);
    }

    [Fact]
    public async Task Declined_payment_keeps_the_order_placed_and_the_stock_reserved()
    {
        var product = TestData.Product(stock: 5);
        var order = await PlaceOrderAsync(product, quantity: 2);

        var act = () => _f.PaymentService.PayAsync(new PayOrderCommand(_f.Customer.Id, order.Id, SimulateFailure: true), default);

        (await act.Should().ThrowAsync<DomainException>()).Which.Code.Should().Be(DomainErrorCodes.PaymentDeclined);
        _f.Store.Orders.Single().Status.Should().Be(OrderStatus.Placed);
        _f.Store.Stock[product.Id].Should().Be(3);
    }

    [Fact]
    public async Task A_declined_payment_can_be_retried()
    {
        var order = await PlaceOrderAsync();
        var declined = () => _f.PaymentService.PayAsync(new PayOrderCommand(_f.Customer.Id, order.Id, SimulateFailure: true), default);
        await declined.Should().ThrowAsync<DomainException>();

        var paid = await _f.PaymentService.PayAsync(new PayOrderCommand(_f.Customer.Id, order.Id, SimulateFailure: false), default);

        paid.Status.Should().Be(OrderStatus.Paid);
    }

    [Fact]
    public async Task Paying_an_already_paid_order_does_not_charge_again()
    {
        var order = await PlaceOrderAsync();
        await _f.PaymentService.PayAsync(new PayOrderCommand(_f.Customer.Id, order.Id, false), default);

        var again = await _f.PaymentService.PayAsync(new PayOrderCommand(_f.Customer.Id, order.Id, false), default);

        again.Status.Should().Be(OrderStatus.Paid);
        _f.PaymentGateway.Charges.Should().ContainSingle();
    }

    [Fact]
    public async Task Cancelling_releases_the_reserved_stock_exactly_once()
    {
        var product = TestData.Product(stock: 5);
        var order = await PlaceOrderAsync(product, quantity: 2);

        var cancelled = await _f.PaymentService.CancelAsync(_f.Customer.Id, order.Id, default);
        await _f.PaymentService.CancelAsync(_f.Customer.Id, order.Id, default);

        cancelled.Status.Should().Be(OrderStatus.Cancelled);
        _f.Store.Stock[product.Id].Should().Be(5);
    }

    [Fact]
    public async Task A_cancelled_order_cannot_be_paid()
    {
        var order = await PlaceOrderAsync();
        await _f.PaymentService.CancelAsync(_f.Customer.Id, order.Id, default);

        var act = () => _f.PaymentService.PayAsync(new PayOrderCommand(_f.Customer.Id, order.Id, false), default);

        (await act.Should().ThrowAsync<DomainException>()).Which.Code.Should().Be(DomainErrorCodes.OrderNotPayable);
        _f.PaymentGateway.Charges.Should().BeEmpty("the gateway must not be called for an unpayable order");
    }

    [Fact]
    public async Task A_paid_order_cannot_be_cancelled()
    {
        var order = await PlaceOrderAsync();
        await _f.PaymentService.PayAsync(new PayOrderCommand(_f.Customer.Id, order.Id, false), default);

        var act = () => _f.PaymentService.CancelAsync(_f.Customer.Id, order.Id, default);

        (await act.Should().ThrowAsync<DomainException>()).Which.Code.Should().Be(DomainErrorCodes.OrderNotCancellable);
    }

    private async Task<OrderDetailsDto> PlaceOrderAsync(IplStore.Domain.Catalog.Product? product = null, int quantity = 1)
    {
        product ??= TestData.Product(stock: 5);
        _f.Store.AddProduct(product);
        _f.Store.PutInCart(_f.Customer.Id, product.Id, quantity);
        var result = await _f.CheckoutService.PlaceOrderAsync(new PlaceOrderCommand(_f.Customer.Id, Guid.NewGuid().ToString()), default);
        return result.Order;
    }
}
