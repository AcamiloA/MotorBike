using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;
using UniversityParking.Api.Controllers;

namespace UniversityParking.Api.OpenApi;

public sealed class IncidentOperationFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        if (context.MethodInfo.DeclaringType != typeof(IncidentsController)) return;
        if (context.MethodInfo.Name == nameof(IncidentsController.Create))
        {
            operation.Description = "GUARD o ADMIN. Adjuntos opcionales Attachments[0], Attachments[1], con índices consecutivos. " +
                "PDF/JPEG/PNG hasta 10 MB por archivo; solicitud hasta 50 MB. ReportedBy se obtiene del usuario autenticado.";
            if (operation.RequestBody?.Content is { } content && content.TryGetValue("multipart/form-data", out var form) && form.Schema is OpenApiSchema schema)
            {
                schema.Properties ??= new Dictionary<string, IOpenApiSchema>();
                schema.Properties["Attachments"] = new OpenApiSchema { Type = JsonSchemaType.Array,
                    Items = new OpenApiSchema { Type = JsonSchemaType.String, Format = "binary" },
                    Description = "Enviar como Attachments[0], Attachments[1], ..." };
            }
        }
        if (context.MethodInfo.Name != nameof(IncidentsController.Attachment)) return;
        operation.Responses ??= [];
        var response = new OpenApiResponse { Description = "Adjunto privado autorizado.", Content = new Dictionary<string, OpenApiMediaType>() };
        foreach (var mime in new[] { "application/pdf", "image/jpeg", "image/png" })
            response.Content[mime] = new() { Schema = new OpenApiSchema { Type = JsonSchemaType.String, Format = "binary" } };
        operation.Responses["200"] = response;
        operation.Responses["302"] = new OpenApiResponse { Description = "URL S3 firmada temporal tras autorización." };
    }
}
