using IplStore.Domain.Catalog;
using IplStore.Domain.Customers;

namespace IplStore.UnitTests.Fakes;

/// <summary>Object Mother: readable, intention-revealing test data.</summary>
public static class TestData
{
    public static readonly DateTimeOffset Now = new(2026, 9, 24, 10, 0, 0, TimeSpan.Zero);

    public static readonly Franchise Csk = new(Guid.NewGuid(), "CSK", "Chennai Super Kings", "Chennai", "#F9CD05");

    public static readonly ProductCategory Jersey = new(Guid.NewGuid(), "JERSEY", "Jersey");

    public static Customer Customer(string name = "Aarav Sharma") =>
        new(Guid.NewGuid(), $"{name.Replace(' ', '.').ToLowerInvariant()}@example.com", name, Now);

    public static Product Product(decimal price = 1000m, int stock = 10, string name = "CSK Home Jersey")
    {
        var id = Guid.NewGuid();
        var product = new Product(id, $"SKU-{id:N}"[..20], name, "desc", Csk.Id, Jersey.Id, price, "INR", stock, Now);
        product.AttachReferences(Csk, Jersey);
        return product;
    }
}
