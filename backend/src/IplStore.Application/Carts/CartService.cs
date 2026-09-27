using IplStore.Application.Catalog;
using IplStore.Application.Common;
using IplStore.Application.Customers;
using IplStore.Application.Orders;
using IplStore.Application.Pricing;
using IplStore.Domain.Common;
using Microsoft.Extensions.Options;

namespace IplStore.Application.Carts;

/// <summary>Use cases for the shopping cart. Every mutation returns the refreshed cart.</summary>
public interface ICartService
{
    Task<CartDto> GetAsync(Guid customerId, CancellationToken cancellationToken);

    Task<CartDto> AddItemAsync(AddCartItemCommand command, CancellationToken cancellationToken);

    Task<CartDto> UpdateItemAsync(UpdateCartItemCommand command, CancellationToken cancellationToken);

    Task<CartDto> RemoveItemAsync(RemoveCartItemCommand command, CancellationToken cancellationToken);
}

/// <inheritdoc />
public sealed class CartService : ICartService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICartRepository _carts;
    private readonly ICartQueries _cartQueries;
    private readonly IProductRepository _products;
    private readonly ICustomerRepository _customers;
    private readonly IPricingPolicy _pricing;
    private readonly IOptionsMonitor<CartOptions> _options;
    private readonly TimeProvider _time;

    public CartService(
        IUnitOfWork unitOfWork,
        ICartRepository carts,
        ICartQueries cartQueries,
        IProductRepository products,
        ICustomerRepository customers,
        IPricingPolicy pricing,
        IOptionsMonitor<CartOptions> options,
        TimeProvider time)
    {
        _unitOfWork = unitOfWork;
        _carts = carts;
        _cartQueries = cartQueries;
        _products = products;
        _customers = customers;
        _pricing = pricing;
        _options = options;
        _time = time;
    }

    public async Task<CartDto> GetAsync(Guid customerId, CancellationToken cancellationToken)
    {
        var view = await _cartQueries.GetAsync(customerId, cancellationToken);
        return ToDto(view);
    }

    public async Task<CartDto> AddItemAsync(AddCartItemCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (command.Quantity <= 0)
        {
            throw new RequestValidationException(nameof(command.Quantity), "Quantity must be greater than zero.");
        }

        await _unitOfWork.ExecuteInTransactionAsync(
            async ct =>
            {
                await EnsureCustomerExistsAsync(command.CustomerId, ct);
                var product = await _products.FindAsync(command.ProductId, ct);
                if (product is null || !product.IsActive)
                {
                    throw new EntityNotFoundException("Product", command.ProductId);
                }

                var options = _options.CurrentValue;
                var cart = await _carts.GetOrCreateForUpdateAsync(command.CustomerId, ct);
                var line = cart.AddItem(product.Id, command.Quantity, options.ToPolicy(), _time.GetUtcNow());

                if (options.ValidateStockOnAdd && !product.CanFulfil(line.Quantity))
                {
                    // Throwing rolls back the in-memory change: nothing is persisted.
                    throw new DomainException(
                        DomainErrorCodes.InsufficientStock,
                        $"Only {product.StockQuantity} unit(s) of '{product.Name}' are available.");
                }
            },
            cancellationToken);

        return await GetAsync(command.CustomerId, cancellationToken);
    }

    public async Task<CartDto> UpdateItemAsync(UpdateCartItemCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (command.Quantity < 0)
        {
            throw new RequestValidationException(nameof(command.Quantity), "Quantity must not be negative.");
        }

        await _unitOfWork.ExecuteInTransactionAsync(
            async ct =>
            {
                await EnsureCustomerExistsAsync(command.CustomerId, ct);
                var options = _options.CurrentValue;
                var cart = await _carts.GetOrCreateForUpdateAsync(command.CustomerId, ct);

                if (command.Quantity > 0 && options.ValidateStockOnAdd)
                {
                    var product = await _products.FindAsync(command.ProductId, ct);
                    if (product is null || !product.CanFulfil(command.Quantity))
                    {
                        throw new DomainException(
                            DomainErrorCodes.InsufficientStock,
                            $"Only {product?.StockQuantity ?? 0} unit(s) are available.");
                    }
                }

                cart.SetItemQuantity(command.ProductId, command.Quantity, options.ToPolicy(), _time.GetUtcNow());
            },
            cancellationToken);

        return await GetAsync(command.CustomerId, cancellationToken);
    }

    public async Task<CartDto> RemoveItemAsync(RemoveCartItemCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        await _unitOfWork.ExecuteInTransactionAsync(
            async ct =>
            {
                await EnsureCustomerExistsAsync(command.CustomerId, ct);
                var cart = await _carts.GetOrCreateForUpdateAsync(command.CustomerId, ct);
                cart.RemoveItem(command.ProductId, _time.GetUtcNow());
            },
            cancellationToken);

        return await GetAsync(command.CustomerId, cancellationToken);
    }

    private async Task EnsureCustomerExistsAsync(Guid customerId, CancellationToken cancellationToken)
    {
        _ = await _customers.FindAsync(customerId, cancellationToken)
            ?? throw new EntityNotFoundException("Customer", customerId);
    }

    private CartDto ToDto(CartView? view)
    {
        var lines = view?.Lines ?? Array.Empty<CartLineView>();

        // Price with the SAME policy checkout uses, so the preview is what the customer pays.
        var price = _pricing.Calculate(new PricingRequest(
            lines.Where(l => l.IsActive)
                 .Select(l => new PricingLine(l.ProductId, l.UnitPrice, l.Quantity))
                 .ToList()));

        var lineDtos = lines
            .Select(l => new CartLineDto(
                l.ProductId,
                l.Sku,
                l.ProductName,
                l.FranchiseCode,
                l.FranchiseName,
                l.CategoryName,
                l.UnitPrice,
                l.Quantity,
                l.UnitPrice * l.Quantity,
                IsAvailable: l.IsActive && l.StockQuantity >= l.Quantity,
                AvailableStock: l.StockQuantity))
            .ToList();

        return new CartDto(
            view?.CartId,
            lineDtos,
            lineDtos.Sum(l => l.Quantity),
            PriceSummaryDto.From(price),
            view?.UpdatedAt);
    }
}
