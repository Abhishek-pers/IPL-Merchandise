using IplStore.Application.Orders;

namespace IplStore.Application.Carts;

// ---------------------------------------------------------------- commands (input)

/// <summary>
/// Add units of a product to the customer's cart (merges with an existing line).
/// Adding INCREMENTS the quantity, so a retry must carry the same <paramref name="IdempotencyKey"/>:
/// a repeat with a key that was already applied changes nothing. Null = no de-duplication.
/// </summary>
public sealed record AddCartItemCommand(Guid CustomerId, Guid ProductId, int Quantity, string? IdempotencyKey = null);

/// <summary>Set the absolute quantity of a line; 0 removes it.</summary>
public sealed record UpdateCartItemCommand(Guid CustomerId, Guid ProductId, int Quantity);

public sealed record RemoveCartItemCommand(Guid CustomerId, Guid ProductId);

// ---------------------------------------------------------------- views (output)

public sealed record CartLineDto(
    Guid ProductId,
    string Sku,
    string ProductName,
    string FranchiseCode,
    string FranchiseName,
    string CategoryName,
    decimal UnitPrice,
    int Quantity,
    decimal LineTotal,
    bool IsAvailable,
    int AvailableStock);

public sealed record CartDto(
    Guid? CartId,
    IReadOnlyList<CartLineDto> Lines,
    int TotalQuantity,
    PriceSummaryDto Price,
    DateTimeOffset? UpdatedAt);

// ---------------------------------------------------------------- read-side projection

/// <summary>A cart line joined with CURRENT catalogue data (price, availability).</summary>
public sealed record CartLineView(
    Guid ProductId,
    string Sku,
    string ProductName,
    string FranchiseCode,
    string FranchiseName,
    string CategoryName,
    decimal UnitPrice,
    int Quantity,
    bool IsActive,
    int StockQuantity);

public sealed record CartView(Guid CartId, DateTimeOffset UpdatedAt, IReadOnlyList<CartLineView> Lines);
