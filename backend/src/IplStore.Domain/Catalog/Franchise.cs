using IplStore.Domain.Common;

namespace IplStore.Domain.Catalog;

/// <summary>An IPL team (CSK, MI, RCB ...). Reference data.</summary>
public sealed class Franchise
{
    private Franchise()
    {
        // Required by the ORM.
    }

    public Franchise(Guid id, string code, string name, string city, string primaryColor)
    {
        Id = Guard.NotEmpty(id, nameof(id));
        Code = Guard.NotBlank(code, nameof(code), 8).ToUpperInvariant();
        Name = Guard.NotBlank(name, nameof(name), 100);
        City = Guard.NotBlank(city, nameof(city), 60);
        PrimaryColor = Guard.NotBlank(primaryColor, nameof(primaryColor), 7);
    }

    public Guid Id { get; private set; }

    public string Code { get; private set; } = string.Empty;

    public string Name { get; private set; } = string.Empty;

    public string City { get; private set; } = string.Empty;

    public string PrimaryColor { get; private set; } = string.Empty;
}
