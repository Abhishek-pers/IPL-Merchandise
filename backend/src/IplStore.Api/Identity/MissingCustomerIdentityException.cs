namespace IplStore.Api.Identity;

/// <summary>Missing or malformed caller identity. Mapped to HTTP 401.</summary>
public sealed class MissingCustomerIdentityException : Exception
{
    public MissingCustomerIdentityException(string message)
        : base(message)
    {
    }
}
