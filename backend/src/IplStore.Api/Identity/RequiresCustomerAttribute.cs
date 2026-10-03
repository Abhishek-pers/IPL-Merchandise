namespace IplStore.Api.Identity;

/// <summary>Marks controllers/actions that need a caller identity (used by Swagger to show the header).</summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class RequiresCustomerAttribute : Attribute
{
}
