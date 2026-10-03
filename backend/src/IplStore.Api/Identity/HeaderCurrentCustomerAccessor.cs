namespace IplStore.Api.Identity;

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
