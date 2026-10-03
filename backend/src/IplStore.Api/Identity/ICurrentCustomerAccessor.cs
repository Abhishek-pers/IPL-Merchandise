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
