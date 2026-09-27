using System.ComponentModel.DataAnnotations;

namespace IplStore.Application.Pricing;

/// <summary>
/// Every pricing knob in one object, bound from the "Pricing" configuration section and
/// read through <c>IOptionsMonitor</c> - edit appsettings.json and the next request uses the
/// new values without a restart.
/// </summary>
public sealed class PricingOptions
{
    public const string SectionName = "Pricing";

    [Required]
    [StringLength(3, MinimumLength = 3)]
    public string Currency { get; set; } = "INR";

    /// <summary>GST rate as a fraction (0.18 = 18%).</summary>
    [Range(0d, 1d)]
    public decimal TaxRate { get; set; } = 0.18m;

    [Range(0d, 100_000d)]
    public decimal FlatShippingFee { get; set; } = 99m;

    /// <summary>Orders with a subtotal at or above this ship free.</summary>
    [Range(0d, 10_000_000d)]
    public decimal FreeShippingThreshold { get; set; } = 999m;
}
