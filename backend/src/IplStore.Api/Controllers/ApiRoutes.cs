namespace IplStore.Api.Controllers;

/// <summary>
/// URL versioning: breaking changes go to /api/v2 while v1 keeps serving existing clients.
/// </summary>
internal static class ApiRoutes
{
    public const string V1 = "api/v1";
}
