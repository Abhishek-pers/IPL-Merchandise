using IplStore.Domain.Common;
using IplStore.Domain.Orders;
using IplStore.UnitTests.Fakes;

namespace IplStore.UnitTests.Domain;

public sealed class OrderTests
{
    private static OrderLine Line(decimal price = 100m, int quantity = 2, Guid? productId = null) =>
        new(productId ?? Guid.NewGuid(), "CSK-JER-H", "CSK Home Jersey", "Chennai Super Kings", "Jersey", price, quantity);

    private static OrderPlacement Placement(IReadOnlyList<OrderLine> lines, PriceBreakdown? price = null) =>
        new(
            OrderId: Guid.NewGuid(),
            OrderNumber: "IPL-20260924-ABCDEFGH",
            CustomerId: Guid.NewGuid(),
            CustomerName: "Aarav Sharma",
            CustomerEmail: "aarav@example.com",
            IdempotencyKey: "key-1",
            Lines: lines,
            Price: price ?? new PriceBreakdown(lines.Sum(l => l.LineTotal), 36m, 99m, "INR"),
            PlacedAt: TestData.Now);

    [Fact]
    public void Place_builds_an_order_with_snapshot_lines_and_denormalised_totals()
    {
        var lines = new[] { Line(100m, 2), Line(50m, 1) };

        var order = Order.Place(Placement(lines));

        order.Status.Should().Be(OrderStatus.Placed);
        order.Price.Subtotal.Should().Be(250m);
        order.Total.Should().Be(250m + 36m + 99m);
        order.ItemCount.Should().Be(3);
        order.Items.Should().HaveCount(2)
            .And.OnlyContain(i => i.OrderId == order.Id && i.CustomerId == order.CustomerId && i.FranchiseName == "Chennai Super Kings");
    }

    [Fact]
    public void Place_without_lines_is_rejected()
    {
        var act = () => Order.Place(Placement(Array.Empty<OrderLine>(), PriceBreakdown.Zero("INR")));

        act.Should().Throw<DomainException>().Which.Code.Should().Be(DomainErrorCodes.OrderHasNoLines);
    }

    [Fact]
    public void Place_with_the_same_product_twice_is_rejected()
    {
        var productId = Guid.NewGuid();

        var act = () => Order.Place(Placement(new[] { Line(productId: productId), Line(productId: productId) }));

        act.Should().Throw<DomainException>().Which.Code.Should().Be(DomainErrorCodes.OrderDuplicateLines);
    }

    [Fact]
    public void Place_rejects_a_price_that_does_not_match_its_lines()
    {
        var lines = new[] { Line(100m, 1) };

        var act = () => Order.Place(Placement(lines, new PriceBreakdown(999m, 0m, 0m, "INR")));

        act.Should().Throw<DomainException>().Which.Code.Should().Be(DomainErrorCodes.OrderPricingMismatch);
    }
}
