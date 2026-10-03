using System.ComponentModel.DataAnnotations;
using IplStore.Application.Catalog;

namespace IplStore.Api.Contracts;

/// <summary>
/// Query-string model for GET /api/v1/products.
/// Example: <c>?search=jersey&amp;franchise=CSK,MI&amp;category=JERSEY&amp;minPrice=500&amp;sort=PriceLowToHigh&amp;page=1&amp;pageSize=12</c>
/// </summary>
public sealed class SearchProductsRequest
{
    /// <summary>Free text: matches name, SKU, franchise and product type.</summary>
    public string? Search { get; init; }

    /// <summary>Franchise code(s), comma separated or repeated (franchise=CSK&amp;franchise=MI).</summary>
    public string[]? Franchise { get; init; }

    /// <summary>Category code(s): JERSEY, CAP, FLAG, AUTOGRAPHED_PHOTO, ACCESSORY.</summary>
    public string[]? Category { get; init; }

    [Range(0d, 10_000_000d)]
    public decimal? MinPrice { get; init; }

    [Range(0d, 10_000_000d)]
    public decimal? MaxPrice { get; init; }

    public bool InStockOnly { get; init; }

    public ProductSort Sort { get; init; } = ProductSort.Name;

    [Range(1, int.MaxValue)]
    public int? Page { get; init; }

    [Range(1, 500)]
    public int? PageSize { get; init; }

    public SearchProductsQuery ToQuery() => new()
    {
        Search = Search,
        Franchises = Franchise ?? Array.Empty<string>(),
        Categories = Category ?? Array.Empty<string>(),
        MinPrice = MinPrice,
        MaxPrice = MaxPrice,
        InStockOnly = InStockOnly,
        Sort = Sort,
        Page = Page,
        PageSize = PageSize,
    };
}

/// <summary>POST /api/v1/cart/items</summary>
public sealed class AddCartItemRequest
{
    [Required]
    public Guid ProductId { get; init; }

    /// <summary>Units to add (merged with any existing line). Business limits come from CartOptions.</summary>
    [Range(1, 1000)]
    public int Quantity { get; init; } = 1;
}

/// <summary>PUT /api/v1/cart/items/{productId}</summary>
public sealed class UpdateCartItemRequest
{
    /// <summary>New absolute quantity; 0 removes the line.</summary>
    [Range(0, 1000)]
    public int Quantity { get; init; }
}

/// <summary>Paging query string for list endpoints.</summary>
public sealed class PageQuery
{
    [Range(1, int.MaxValue)]
    public int? Page { get; init; }

    [Range(1, 500)]
    public int? PageSize { get; init; }
}

/// <summary>POST /api/v1/orders/{orderId}/payment</summary>
public sealed class PayOrderRequest
{
    /// <summary>Demo only: true makes the dummy gateway decline the payment.</summary>
    public bool SimulateFailure { get; init; }
}
