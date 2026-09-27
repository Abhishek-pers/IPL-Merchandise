namespace IplStore.Domain.Common;

/// <summary>
/// Single catalogue of business error codes. Clients (and tests) depend on these strings,
/// so they are part of the public contract: add new ones, never rename existing ones.
/// </summary>
public static class DomainErrorCodes
{
    public const string InvalidArgument = "validation.invalid_argument";

    public const string CartLineLimitReached = "cart.line_limit_reached";
    public const string CartQuantityLimitExceeded = "cart.quantity_limit_exceeded";
    public const string CartItemNotFound = "cart.item_not_found";
    public const string CartEmpty = "cart.empty";

    public const string ProductUnavailable = "product.unavailable";
    public const string InsufficientStock = "product.insufficient_stock";

    public const string OrderHasNoLines = "order.no_lines";
    public const string OrderDuplicateLines = "order.duplicate_lines";
    public const string OrderPricingMismatch = "order.pricing_mismatch";
}
