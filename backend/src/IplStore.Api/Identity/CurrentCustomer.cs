namespace IplStore.Api.Identity;

/// <summary>
/// Who is calling? Abstracted so the mechanism can change without touching controllers.
/// </summary>
/// <remarks>
/// Today: the <c>X-Customer-Id</c> header (the assessment has no login requirement).
/// Production: register a JWT-claims implementation (Entra ID / B2C) in
/// <c>Composition/ApiServiceCollectionExtensions.cs</c> instead - one line change.
/// </remarks>
public interface ICurrentCustomerAccessor
{
    Guid GetRequiredCustomerId();
}

/// <summary>Missing or malformed caller identity. Mapped to HTTP 401.</summary>
public sealed class MissingCustomerIdentityException : Exception
{
    public MissingCustomerIdentityException(string message)
        : base(message)
    {
    }
}

/// <summary>Reads the customer id from the <c>X-Customer-Id</c> request header.</summary>
internal sealed class HeaderCurrentCustomerAccessor : ICurrentCustomerAccessor
{
    public const string HeaderName = "X-Customer-Id";

    private readonly IHttpContextAccessor _httpContextAccessor;

    public HeaderCurrentCustomerAccessor(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public Guid GetRequiredCustomerId()
    {
        var headers = _httpContextAccessor.HttpContext?.Request.Headers;
        var raw = headers?[HeaderName].ToString();

        if (string.IsNullOrWhiteSpace(raw))
        {
            throw new MissingCustomerIdentityException($"The {HeaderName} header is required.");
        }

        if (!Guid.TryParse(raw, out var customerId) || customerId == Guid.Empty)
        {
            throw new MissingCustomerIdentityException($"The {HeaderName} header must be a valid, non-empty GUID.");
        }

        return customerId;
    }
}

/// <summary>Marks controllers/actions that need a caller identity (used by Swagger to show the header).</summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class RequiresCustomerAttribute : Attribute
{
}
