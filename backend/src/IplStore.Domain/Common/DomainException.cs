namespace IplStore.Domain.Common;

/// <summary>
/// A business rule was violated (e.g. "cart line limit reached", "insufficient stock").
/// The API maps this to HTTP 422 Unprocessable Entity. <see cref="Code"/> is a stable,
/// machine-readable identifier that clients can switch on; the message is for humans.
/// </summary>
public class DomainException : Exception
{
    public DomainException(string code, string message)
        : base(message)
    {
        Code = code;
    }

    public string Code { get; }
}

/// <summary>
/// The requested entity does not exist (or is not visible to the caller). Mapped to HTTP 404.
/// </summary>
public sealed class EntityNotFoundException : DomainException
{
    public EntityNotFoundException(string entityName, object key)
        : base($"{entityName.ToLowerInvariant()}.not_found", $"{entityName} '{key}' was not found.")
    {
        EntityName = entityName;
        Key = key;
    }

    public string EntityName { get; }

    public object Key { get; }
}
