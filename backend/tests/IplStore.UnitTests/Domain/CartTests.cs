using IplStore.Domain.Carts;
using IplStore.Domain.Common;
using IplStore.UnitTests.Fakes;

namespace IplStore.UnitTests.Domain;

public sealed class CartTests
{
    private static readonly CartPolicy Policy = new(maxQuantityPerLine: 5, maxDistinctLines: 2);

    private readonly Cart _cart = new(Guid.NewGuid(), Guid.NewGuid(), TestData.Now);

    [Fact]
    public void AddItem_new_product_creates_a_line()
    {
        var productId = Guid.NewGuid();

        _cart.AddItem(productId, 2, Policy, TestData.Now);

        _cart.Items.Should().ContainSingle(i => i.ProductId == productId && i.Quantity == 2);
        _cart.Items.Single().CustomerId.Should().Be(_cart.CustomerId, "the shard key is copied onto every line");
    }

    [Fact]
    public void AddItem_same_product_twice_merges_into_one_line_instead_of_duplicating()
    {
        var productId = Guid.NewGuid();

        _cart.AddItem(productId, 1, Policy, TestData.Now);
        _cart.AddItem(productId, 2, Policy, TestData.Now);

        _cart.Items.Should().ContainSingle().Which.Quantity.Should().Be(3);
    }

    [Fact]
    public void AddItem_beyond_the_per_line_limit_is_rejected_and_quantity_is_unchanged()
    {
        var productId = Guid.NewGuid();
        _cart.AddItem(productId, 4, Policy, TestData.Now);

        var act = () => _cart.AddItem(productId, 2, Policy, TestData.Now);

        act.Should().Throw<DomainException>().Which.Code.Should().Be(DomainErrorCodes.CartQuantityLimitExceeded);
        _cart.Items.Single().Quantity.Should().Be(4);
    }

    [Fact]
    public void AddItem_beyond_the_distinct_line_limit_is_rejected()
    {
        _cart.AddItem(Guid.NewGuid(), 1, Policy, TestData.Now);
        _cart.AddItem(Guid.NewGuid(), 1, Policy, TestData.Now);

        var act = () => _cart.AddItem(Guid.NewGuid(), 1, Policy, TestData.Now);

        act.Should().Throw<DomainException>().Which.Code.Should().Be(DomainErrorCodes.CartLineLimitReached);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void AddItem_with_non_positive_quantity_is_rejected(int quantity)
    {
        var act = () => _cart.AddItem(Guid.NewGuid(), quantity, Policy, TestData.Now);

        act.Should().Throw<DomainException>().Which.Code.Should().Be(DomainErrorCodes.InvalidArgument);
    }

    [Fact]
    public void SetItemQuantity_to_zero_removes_the_line()
    {
        var productId = Guid.NewGuid();
        _cart.AddItem(productId, 3, Policy, TestData.Now);

        _cart.SetItemQuantity(productId, 0, Policy, TestData.Now);

        _cart.IsEmpty.Should().BeTrue();
    }

    [Fact]
    public void SetItemQuantity_for_product_not_in_cart_is_rejected()
    {
        var act = () => _cart.SetItemQuantity(Guid.NewGuid(), 1, Policy, TestData.Now);

        act.Should().Throw<DomainException>().Which.Code.Should().Be(DomainErrorCodes.CartItemNotFound);
    }

    [Fact]
    public void Clear_empties_the_cart_and_touches_updated_at()
    {
        _cart.AddItem(Guid.NewGuid(), 1, Policy, TestData.Now);
        var later = TestData.Now.AddMinutes(5);

        _cart.Clear(later);

        _cart.IsEmpty.Should().BeTrue();
        _cart.UpdatedAt.Should().Be(later);
    }
}
