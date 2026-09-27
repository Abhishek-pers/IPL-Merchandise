namespace IplStore.Domain.Carts;

/// <summary>
/// Tunable cart limits, passed INTO the aggregate as one parameter object.
/// The Application layer builds it from <c>CartOptions</c> (appsettings), so changing a limit
/// is a configuration change - no method signature anywhere has to change.
/// </summary>
public sealed record CartPolicy
{
    public CartPolicy(int maxQuantityPerLine, int maxDistinctLines)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxQuantityPerLine);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxDistinctLines);
        MaxQuantityPerLine = maxQuantityPerLine;
        MaxDistinctLines = maxDistinctLines;
    }

    /// <summary>Maximum units of a single product in one cart (anti-hoarding / scalping).</summary>
    public int MaxQuantityPerLine { get; }

    /// <summary>Maximum number of different products in one cart.</summary>
    public int MaxDistinctLines { get; }
}
