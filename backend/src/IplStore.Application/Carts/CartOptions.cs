using System.ComponentModel.DataAnnotations;
using IplStore.Domain.Carts;

namespace IplStore.Application.Carts;

/// <summary>Cart limits. Bound from the "Cart" configuration section.</summary>
public sealed class CartOptions
{
    public const string SectionName = "Cart";

    [Range(1, 1000)]
    public int MaxQuantityPerLine { get; set; } = 10;

    [Range(1, 500)]
    public int MaxDistinctLines { get; set; } = 25;

    /// <summary>
    /// When true, adding more units than are currently in stock is rejected immediately.
    /// Stock is still only RESERVED at checkout; this is a user-experience check.
    /// </summary>
    public bool ValidateStockOnAdd { get; set; } = true;

    /// <summary>Maps configuration onto the domain's policy object.</summary>
    public CartPolicy ToPolicy() => new(MaxQuantityPerLine, MaxDistinctLines);
}
