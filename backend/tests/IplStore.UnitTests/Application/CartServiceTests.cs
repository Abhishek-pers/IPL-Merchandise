using IplStore.Application.Carts;
using IplStore.Domain.Common;
using IplStore.UnitTests.Fakes;

namespace IplStore.UnitTests.Application;

public sealed class CartServiceTests
{
    private readonly UseCaseFixture _f = new();

    [Fact]
    public async Task Adding_the_same_product_twice_yields_one_line_with_summed_quantity()
    {
        var product = TestData.Product(price: 500m, stock: 10);
        _f.Store.AddProduct(product);

        await _f.CartService.AddItemAsync(new AddCartItemCommand(_f.Customer.Id, product.Id, 1), default);
        var cart = await _f.CartService.AddItemAsync(new AddCartItemCommand(_f.Customer.Id, product.Id, 2), default);

        cart.Lines.Should().ContainSingle().Which.Quantity.Should().Be(3);
        cart.Price.Subtotal.Should().Be(1500m);
        cart.Price.Shipping.Should().Be(0m, "subtotal is above the free-shipping threshold");
    }

    [Fact]
    public async Task Adding_more_than_is_in_stock_is_rejected_and_nothing_is_persisted()
    {
        var product = TestData.Product(stock: 2);
        _f.Store.AddProduct(product);

        var act = () => _f.CartService.AddItemAsync(new AddCartItemCommand(_f.Customer.Id, product.Id, 3), default);

        (await act.Should().ThrowAsync<DomainException>()).Which.Code.Should().Be(DomainErrorCodes.InsufficientStock);
        _f.Store.CartLines(_f.Customer.Id).Should().BeEmpty();
    }

    [Fact]
    public async Task Stock_validation_on_add_can_be_switched_off_through_options()
    {
        var product = TestData.Product(stock: 2);
        _f.Store.AddProduct(product);
        _f.CartOptions.CurrentValue = new CartOptions { MaxQuantityPerLine = 10, MaxDistinctLines = 5, ValidateStockOnAdd = false };

        var cart = await _f.CartService.AddItemAsync(new AddCartItemCommand(_f.Customer.Id, product.Id, 3), default);

        cart.Lines.Single().IsAvailable.Should().BeFalse("the UI can warn; checkout will enforce stock");
    }

    [Fact]
    public async Task Unknown_product_is_not_found()
    {
        var act = () => _f.CartService.AddItemAsync(new AddCartItemCommand(_f.Customer.Id, Guid.NewGuid(), 1), default);

        await act.Should().ThrowAsync<EntityNotFoundException>();
    }

    [Fact]
    public async Task Unknown_customer_is_not_found()
    {
        var product = TestData.Product();
        _f.Store.AddProduct(product);

        var act = () => _f.CartService.AddItemAsync(new AddCartItemCommand(Guid.NewGuid(), product.Id, 1), default);

        await act.Should().ThrowAsync<EntityNotFoundException>();
    }

    [Fact]
    public async Task Updating_quantity_to_zero_removes_the_line()
    {
        var product = TestData.Product();
        _f.Store.AddProduct(product);
        await _f.CartService.AddItemAsync(new AddCartItemCommand(_f.Customer.Id, product.Id, 2), default);

        var cart = await _f.CartService.UpdateItemAsync(new UpdateCartItemCommand(_f.Customer.Id, product.Id, 0), default);

        cart.Lines.Should().BeEmpty();
        cart.Price.Total.Should().Be(0m);
    }

    [Fact]
    public async Task Empty_cart_for_a_new_customer_is_returned_not_an_error()
    {
        var cart = await _f.CartService.GetAsync(_f.Customer.Id, default);

        cart.CartId.Should().BeNull();
        cart.Lines.Should().BeEmpty();
    }
}
