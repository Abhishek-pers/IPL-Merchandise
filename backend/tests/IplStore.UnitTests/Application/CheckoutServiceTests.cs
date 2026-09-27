using IplStore.Application.Common;
using IplStore.Application.Orders;
using IplStore.Domain.Common;
using IplStore.UnitTests.Fakes;

namespace IplStore.UnitTests.Application;

public sealed class CheckoutServiceTests
{
    private readonly UseCaseFixture _f = new();

    [Fact]
    public async Task Checkout_creates_an_order_reserves_stock_and_empties_the_cart()
    {
        var jersey = TestData.Product(price: 2499m, stock: 5);
        var cap = TestData.Product(price: 799m, stock: 5, name: "CSK Cap");
        _f.Store.AddProduct(jersey);
        _f.Store.AddProduct(cap);
        _f.Store.PutInCart(_f.Customer.Id, jersey.Id, 2);
        _f.Store.PutInCart(_f.Customer.Id, cap.Id, 1);

        var result = await _f.CheckoutService.PlaceOrderAsync(new PlaceOrderCommand(_f.Customer.Id, "key-1"), default);

        result.IsReplay.Should().BeFalse();
        result.Order.ItemCount.Should().Be(3);
        result.Order.Price.Subtotal.Should().Be(2 * 2499m + 799m);
        result.Order.Price.Total.Should().Be(result.Order.Price.Subtotal + result.Order.Price.Tax + result.Order.Price.Shipping);
        result.Order.Lines.Should().Contain(l => l.ProductName == "CSK Cap" && l.FranchiseName == "Chennai Super Kings");
        _f.Store.Stock[jersey.Id].Should().Be(3);
        _f.Store.Stock[cap.Id].Should().Be(4);
        _f.Store.CartLines(_f.Customer.Id).Should().BeEmpty();
    }

    [Fact]
    public async Task Repeating_checkout_with_the_same_idempotency_key_returns_the_original_order_once()
    {
        var product = TestData.Product(stock: 5);
        _f.Store.AddProduct(product);
        _f.Store.PutInCart(_f.Customer.Id, product.Id, 1);

        var first = await _f.CheckoutService.PlaceOrderAsync(new PlaceOrderCommand(_f.Customer.Id, "same-key"), default);
        var second = await _f.CheckoutService.PlaceOrderAsync(new PlaceOrderCommand(_f.Customer.Id, "same-key"), default);

        second.IsReplay.Should().BeTrue();
        second.Order.Id.Should().Be(first.Order.Id);
        _f.Store.Orders.Should().ContainSingle();
        _f.Store.Stock[product.Id].Should().Be(4, "stock must be reserved exactly once");
    }

    [Fact]
    public async Task Checkout_of_an_empty_cart_is_rejected()
    {
        var act = () => _f.CheckoutService.PlaceOrderAsync(new PlaceOrderCommand(_f.Customer.Id, "key-1"), default);

        (await act.Should().ThrowAsync<DomainException>()).Which.Code.Should().Be(DomainErrorCodes.CartEmpty);
    }

    [Fact]
    public async Task Insufficient_stock_on_any_line_rolls_back_every_reservation()
    {
        var plenty = TestData.Product(stock: 10);
        var scarce = TestData.Product(stock: 1);
        _f.Store.AddProduct(plenty);
        _f.Store.AddProduct(scarce);
        _f.Store.PutInCart(_f.Customer.Id, plenty.Id, 2);
        _f.Store.PutInCart(_f.Customer.Id, scarce.Id, 2);

        var act = () => _f.CheckoutService.PlaceOrderAsync(new PlaceOrderCommand(_f.Customer.Id, "key-1"), default);

        (await act.Should().ThrowAsync<DomainException>()).Which.Code.Should().Be(DomainErrorCodes.InsufficientStock);
        _f.Store.Stock[plenty.Id].Should().Be(10);
        _f.Store.Stock[scarce.Id].Should().Be(1);
        _f.Store.Orders.Should().BeEmpty();
        _f.Store.CartLines(_f.Customer.Id).Should().HaveCount(2, "the cart is kept so the customer can adjust it");
    }

    [Fact]
    public async Task Missing_idempotency_key_is_rejected_when_required()
    {
        var act = () => _f.CheckoutService.PlaceOrderAsync(new PlaceOrderCommand(_f.Customer.Id, null), default);

        await act.Should().ThrowAsync<RequestValidationException>();
    }

    [Fact]
    public async Task Missing_idempotency_key_is_generated_when_not_required()
    {
        var product = TestData.Product();
        _f.Store.AddProduct(product);
        _f.Store.PutInCart(_f.Customer.Id, product.Id, 1);
        _f.CheckoutOptions.CurrentValue = new CheckoutOptions { RequireIdempotencyKey = false };

        var result = await _f.CheckoutService.PlaceOrderAsync(new PlaceOrderCommand(_f.Customer.Id, null), default);

        result.IsReplay.Should().BeFalse();
        _f.Store.Orders.Single().IdempotencyKey.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Placed_order_shows_up_in_order_history()
    {
        var product = TestData.Product();
        _f.Store.AddProduct(product);
        _f.Store.PutInCart(_f.Customer.Id, product.Id, 1);
        var placed = await _f.CheckoutService.PlaceOrderAsync(new PlaceOrderCommand(_f.Customer.Id, "k"), default);

        var history = await _f.OrderService.ListAsync(_f.Customer.Id, page: null, pageSize: null, default);

        history.Items.Should().ContainSingle().Which.Id.Should().Be(placed.Order.Id);
    }

    [Fact]
    public async Task Another_customers_order_is_not_found()
    {
        var product = TestData.Product();
        _f.Store.AddProduct(product);
        _f.Store.PutInCart(_f.Customer.Id, product.Id, 1);
        var placed = await _f.CheckoutService.PlaceOrderAsync(new PlaceOrderCommand(_f.Customer.Id, "k"), default);

        var act = () => _f.OrderService.GetAsync(Guid.NewGuid(), placed.Order.Id, default);

        await act.Should().ThrowAsync<EntityNotFoundException>();
    }
}
