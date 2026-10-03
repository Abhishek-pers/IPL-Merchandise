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
