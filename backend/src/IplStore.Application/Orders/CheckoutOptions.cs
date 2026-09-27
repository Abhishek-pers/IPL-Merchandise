using System.ComponentModel.DataAnnotations;

namespace IplStore.Application.Orders;

/// <summary>Checkout behaviour. Bound from the "Checkout" configuration section.</summary>
public sealed class CheckoutOptions
{
    public const string SectionName = "Checkout";

    /// <summary>
    /// When true (recommended, default) a checkout without an <c>Idempotency-Key</c> header is
    /// rejected with 400. Set false to let the server generate a key (convenient for manual
    /// testing in Swagger, but then client retries are NOT protected against duplicates).
    /// </summary>
    public bool RequireIdempotencyKey { get; set; } = true;

    [Range(8, 100)]
    public int MaxIdempotencyKeyLength { get; set; } = 100;
}
