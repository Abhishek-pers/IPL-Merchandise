namespace IplStore.Domain.Common;

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
