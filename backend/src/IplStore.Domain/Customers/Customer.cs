using IplStore.Domain.Common;

namespace IplStore.Domain.Customers;

/// <summary>A shopper. Authentication is out of scope (see docs/07 roadmap: Entra ID / JWT).</summary>
public sealed class Customer
{
    private Customer()
    {
        // Required by the ORM.
    }

    public Customer(Guid id, string email, string fullName, DateTimeOffset createdAt)
    {
        Id = Guard.NotEmpty(id, nameof(id));
        Email = Guard.NotBlank(email, nameof(email), 254).Trim();
        FullName = Guard.NotBlank(fullName, nameof(fullName), 150).Trim();
        CreatedAt = createdAt;
    }

    public Guid Id { get; private set; }

    public string Email { get; private set; } = string.Empty;

    public string FullName { get; private set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; private set; }
}
