namespace IplStore.Application.Orders;

/// <summary>Strategy for human-friendly order numbers (e.g. IPL-20260924-7KQ2M9XA).</summary>
public interface IOrderNumberGenerator
{
    string Next(DateTimeOffset placedAt);
}
