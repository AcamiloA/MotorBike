using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace UniversityParking.Api.OpenApi;

public sealed class AuthorizationOperationFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        var attributes = context.MethodInfo.GetCustomAttributes(true)
            .Concat(context.MethodInfo.DeclaringType?.GetCustomAttributes(true) ?? []);
        var authorization = attributes.OfType<IAuthorizeData>().ToArray();
        if (attributes.OfType<IAllowAnonymous>().Any() || authorization.Length == 0) return;
        operation.Security = [new OpenApiSecurityRequirement { [new OpenApiSecuritySchemeReference("Bearer", context.Document)] = [] }];
        operation.Responses ??= [];
        operation.Responses.TryAdd("401", new OpenApiResponse { Description = "Sesión inválida o vencida." });
        operation.Responses.TryAdd("403", new OpenApiResponse { Description = "Permisos insuficientes." });
    }
}
