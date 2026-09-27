using System.Globalization;
using System.Security.Cryptography;
using IplStore.Application.Orders;

namespace IplStore.Infrastructure.Orders;

/// <summary>
/// IPL-yyyyMMdd-XXXXXXXX with 8 cryptographically random characters from a 32-symbol alphabet
/// without look-alikes (0/O, 1/I). 32^8 ~ 1.1e12 per day, so collisions are practically
/// impossible; uq_orders_order_number is the backstop. No DB sequence = no hot spot, works
/// unchanged on a sharded database.
/// </summary>
internal sealed class OrderNumberGenerator : IOrderNumberGenerator
{
    private const string Alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";

    public string Next(DateTimeOffset placedAt) =>
        $"IPL-{placedAt.UtcDateTime.ToString("yyyyMMdd", CultureInfo.InvariantCulture)}-{RandomNumberGenerator.GetString(Alphabet, 8)}";
}
