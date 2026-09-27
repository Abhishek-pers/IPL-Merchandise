using System.Net;
using IplStore.Application.Carts;
using IplStore.Application.Common;
using IplStore.Application.Orders;
using IplStore.IntegrationTests.Infrastructure;

namespace IplStore.IntegrationTests.Api;

/// <summary>
/// The guarantees the system is built around, proven with real parallel HTTP requests against
/// real PostgreSQL: no duplicate rows, no overselling, exactly-once checkout.
/// </summary>
public sealed class ConcurrencyTests : ApiTestBase
{
    public ConcurrencyTests(PostgresFixture postgres)
        : base(postgres)
    {
    }

    [Fact]
    public async Task Parallel_first_requests_create_exactly_one_cart_and_one_line()
    {
        var customer = await CreateCustomerAsync();
        var product = await ProductIdAsync("MI-CAP");

        // 10 concurrent "add 1" requests for the same product, customer has no cart yet.
        var responses = await Task.WhenAll(Enumerable.Range(0, 10).Select(_ =>
            Client.SendAsync(Request(HttpMethod.Post, "/api/v1/cart/items", customer, new { productId = product, quantity = 1 }))));

        responses.Should().OnlyContain(r => r.StatusCode == HttpStatusCode.OK);
        (await ScalarAsync<long>($"SELECT count(*) FROM carts WHERE customer_id = '{customer}'")).Should().Be(1);
        (await ScalarAsync<long>($"SELECT count(*) FROM cart_items WHERE customer_id = '{customer}'")).Should().Be(1);
        (await ScalarAsync<int>($"SELECT quantity FROM cart_items WHERE customer_id = '{customer}'")).Should().Be(10, "no update may be lost");
    }

    [Fact]
    public async Task Same_idempotency_key_sent_concurrently_creates_exactly_one_order()
    {
        var customer = await CreateCustomerAsync();
        var product = await ProductIdAsync("KKR-FLG");
        var stockBefore = await ScalarAsync<int>($"SELECT stock_quantity FROM products WHERE id = '{product}'");
        await Client.SendAsync(Request(HttpMethod.Post, "/api/v1/cart/items", customer, new { productId = product, quantity = 2 }));

        var responses = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ =>
            Client.SendAsync(Request(HttpMethod.Post, "/api/v1/orders", customer, idempotencyKey: "double-click-key"))));

        responses.Count(r => r.StatusCode == HttpStatusCode.Created).Should().Be(1);
        responses.Count(r => r.StatusCode == HttpStatusCode.OK).Should().Be(7, "every repeat is an idempotent replay");
        var orderIds = await Task.WhenAll(responses.Select(async r => (await ReadAsync<OrderDetailsDto>(r)).Id));
        orderIds.Distinct().Should().ContainSingle();
        (await ScalarAsync<long>($"SELECT count(*) FROM orders WHERE customer_id = '{customer}'")).Should().Be(1);
        (await ScalarAsync<int>($"SELECT stock_quantity FROM products WHERE id = '{product}'")).Should().Be(stockBefore - 2);
    }

    [Fact]
    public async Task Concurrent_checkouts_never_oversell_limited_stock()
    {
        const int stock = 3;
        const int buyers = 8;
        var product = await ProductIdAsync("DC-AUT");
        await SetStockAsync(product, 50); // let every buyer put it in the cart first

        var customers = new List<Guid>();
        for (var i = 0; i < buyers; i++)
        {
            var customer = await CreateCustomerAsync();
            await Client.SendAsync(Request(HttpMethod.Post, "/api/v1/cart/items", customer, new { productId = product, quantity = 1 }));
            customers.Add(customer);
        }

        await SetStockAsync(product, stock);

        var responses = await Task.WhenAll(customers.Select(c =>
            Client.SendAsync(Request(HttpMethod.Post, "/api/v1/orders", c, idempotencyKey: $"checkout-{c}"))));

        responses.Count(r => r.StatusCode == HttpStatusCode.Created).Should().Be(stock);
        responses.Count(r => r.StatusCode == HttpStatusCode.UnprocessableEntity).Should().Be(buyers - stock);
        (await ScalarAsync<int>($"SELECT stock_quantity FROM products WHERE id = '{product}'")).Should().Be(0);
        (await ScalarAsync<int>($"SELECT stock_quantity FROM product_catalog WHERE product_id = '{product}'"))
            .Should().Be(0, "the read model is updated in the same transaction");
    }

    [Fact]
    public async Task Order_history_lists_the_customers_orders_and_hides_other_customers_orders()
    {
        var customer = await CreateCustomerAsync();
        var stranger = await CreateCustomerAsync();
        var product = await ProductIdAsync("SRH-MUG");
        await Client.SendAsync(Request(HttpMethod.Post, "/api/v1/cart/items", customer, new { productId = product, quantity = 1 }));
        var placed = await ReadAsync<OrderDetailsDto>(
            await Client.SendAsync(Request(HttpMethod.Post, "/api/v1/orders", customer, idempotencyKey: Guid.NewGuid().ToString())));

        var history = await ReadAsync<PagedResult<OrderSummaryDto>>(
            await Client.SendAsync(Request(HttpMethod.Get, "/api/v1/orders", customer)));
        var peek = await Client.SendAsync(Request(HttpMethod.Get, $"/api/v1/orders/{placed.Id}", stranger));
        var cart = await ReadAsync<CartDto>(await Client.SendAsync(Request(HttpMethod.Get, "/api/v1/cart", customer)));

        history.Items.Should().ContainSingle().Which.OrderNumber.Should().Be(placed.OrderNumber);
        peek.StatusCode.Should().Be(HttpStatusCode.NotFound);
        cart.Lines.Should().BeEmpty("checkout empties the cart");
    }

    [Fact]
    public async Task Requests_without_customer_identity_are_401()
    {
        var response = await Client.GetAsync("/api/v1/cart");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
