using IplStore.Application.Carts;
using IplStore.Application.Common;
using IplStore.Application.Orders;
using IplStore.Application.Pricing;
using IplStore.Domain.Customers;
using IplStore.UnitTests.Fakes;
using Microsoft.Extensions.Logging.Abstractions;

namespace IplStore.UnitTests.Application;

/// <summary>Wires the real use-case services to in-memory fakes (manual DI - no container needed).</summary>
public sealed class UseCaseFixture
{
    public UseCaseFixture()
    {
        Customer = TestData.Customer();
        Store.AddCustomer(Customer);

        var pricing = new StandardPricingPolicy(PricingOptions);
        var carts = new FakeCartRepository(Store, Time);
        var products = new FakeProductRepository(Store);
        var customers = new FakeCustomerRepository(Store);

        CartService = new CartService(
            UnitOfWork, carts, new FakeCartQueries(Store), products, customers, pricing, new FakeIdempotencyStore(Store), CartOptions, Time);

        CheckoutService = new CheckoutService(
            UnitOfWork,
            carts,
            products,
            new FakeOrderRepository(Store),
            new FakeOrderQueries(Store),
            customers,
            pricing,
            new SequentialOrderNumberGenerator(),
            CheckoutOptions,
            Time,
            NullLogger<CheckoutService>.Instance);

        OrderService = new OrderService(new FakeOrderQueries(Store), new TestOptionsMonitor<PagingOptions>(new PagingOptions()));

        PaymentService = new PaymentService(
            UnitOfWork,
            new FakeOrderRepository(Store),
            new FakeOrderQueries(Store),
            products,
            PaymentGateway,
            NullLogger<PaymentService>.Instance);
    }

    public InMemoryStore Store { get; } = new();

    public FixedTimeProvider Time { get; } = new(TestData.Now);

    public FakeUnitOfWork UnitOfWork => _unitOfWork ??= new FakeUnitOfWork(Store);

    public TestOptionsMonitor<CartOptions> CartOptions { get; } = new(new CartOptions { MaxQuantityPerLine = 10, MaxDistinctLines = 5 });

    public TestOptionsMonitor<CheckoutOptions> CheckoutOptions { get; } = new(new CheckoutOptions());

    public TestOptionsMonitor<PricingOptions> PricingOptions { get; } = new(new PricingOptions
    {
        Currency = "INR",
        TaxRate = 0.18m,
        FlatShippingFee = 99m,
        FreeShippingThreshold = 999m,
    });

    public Customer Customer { get; }

    public CartService CartService { get; }

    public CheckoutService CheckoutService { get; }

    public OrderService OrderService { get; }

    public FakePaymentGateway PaymentGateway { get; } = new();

    public PaymentService PaymentService { get; }

    private FakeUnitOfWork? _unitOfWork;
}
