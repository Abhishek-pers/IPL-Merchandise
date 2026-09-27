using System.Reflection;
using IplStore.Api.Identity;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace IplStore.Api.Composition;

/// <summary>Shows the X-Customer-Id header in Swagger UI for endpoints marked [RequiresCustomer].</summary>
internal sealed class SwaggerCustomerHeaderFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        var requiresCustomer =
            context.MethodInfo.GetCustomAttribute<RequiresCustomerAttribute>() is not null ||
            context.MethodInfo.DeclaringType?.GetCustomAttribute<RequiresCustomerAttribute>() is not null;

        if (!requiresCustomer)
        {
            return;
        }

        operation.Parameters ??= new List<OpenApiParameter>();
        operation.Parameters.Add(new OpenApiParameter
        {
            Name = HeaderCurrentCustomerAccessor.HeaderName,
            In = ParameterLocation.Header,
            Required = true,
            Description = "Demo identity. Try 11111111-1111-1111-1111-111111111111 (Aarav) or 22222222-2222-2222-2222-222222222222 (Priya).",
            Schema = new OpenApiSchema { Type = "string", Format = "uuid" },
        });
    }
}
