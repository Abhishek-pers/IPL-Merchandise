using IplStore.Domain.Common;

namespace IplStore.Domain.Catalog;

/// <summary>
/// Product type (Jersey, Cap, Flag, Autographed Photo ...). Modelled as DATA rather than an
/// enum so a new category is an INSERT, not a code change + deployment (Open/Closed).
/// </summary>
public sealed class ProductCategory
{
    private ProductCategory()
    {
        // Required by the ORM.
    }

    public ProductCategory(Guid id, string code, string name)
    {
        Id = Guard.NotEmpty(id, nameof(id));
        Code = Guard.NotBlank(code, nameof(code), 40).ToUpperInvariant();
        Name = Guard.NotBlank(name, nameof(name), 80);
    }

    public Guid Id { get; private set; }

    public string Code { get; private set; } = string.Empty;

    public string Name { get; private set; } = string.Empty;
}
