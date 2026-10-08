using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;
using UniversityParking.Api.Controllers;

namespace UniversityParking.Api.OpenApi;

public sealed class VehicleFileOperationFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        if (context.MethodInfo.DeclaringType != typeof(VehiclesController)) return;
        var method = context.MethodInfo.Name;
        if (method is nameof(VehiclesController.Register) or nameof(VehiclesController.Renew))
        {
            operation.Description = "multipart/form-data con índices consecutivos: Photos[0].Type=GENERAL, Photos[0].File; " +
                "Documents[0].Type, Documents[0].DocumentNumber, Documents[0].IssuedOn, Documents[0].ExpiresOn y Documents[0].File. " +
                "Fotografías JPEG/PNG hasta 5 MB; documentos PDF/JPEG/PNG hasta 10 MB; solicitud hasta 50 MB. " +
                (method == nameof(VehiclesController.Renew) ? "Renovación permite cuerpo vacío si los soportes existentes son suficientes." : "Se requieren fotografía GENERAL y los documentos del tipo de vehículo.");
            var schema = new OpenApiSchema { Type = JsonSchemaType.Object, Properties = new Dictionary<string, IOpenApiSchema>(), Required = new HashSet<string>() };
            if (method == nameof(VehiclesController.Register))
            {
                foreach (var name in new[] { "Type", "Plate", "FrameNumber", "Brand", "Model", "Color" })
                    schema.Properties[name] = new OpenApiSchema { Type = JsonSchemaType.String };
                foreach (var name in new[] { "Type", "Brand", "Model", "Color", "Photos[0].Type", "Photos[0].File" }) schema.Required.Add(name);
                schema.Properties["Photos[0].Type"] = new OpenApiSchema { Type = JsonSchemaType.String, Description = "GENERAL" };
                schema.Properties["Photos[0].File"] = new OpenApiSchema { Type = JsonSchemaType.String, Format = "binary" };
            }
            for (var index = 0; index < 2; index++)
            {
                schema.Properties[$"Documents[{index}].Type"] = new OpenApiSchema { Type = JsonSchemaType.String,
                    Description = "VEHICLE_REGISTRATION, INSURANCE u OWNERSHIP_SUPPORT. Omitir el segundo bloque para bicicleta." };
                schema.Properties[$"Documents[{index}].File"] = new OpenApiSchema { Type = JsonSchemaType.String, Format = "binary" };
                schema.Properties[$"Documents[{index}].DocumentNumber"] = new OpenApiSchema { Type = JsonSchemaType.String };
                schema.Properties[$"Documents[{index}].IssuedOn"] = new OpenApiSchema { Type = JsonSchemaType.String, Format = "date" };
                schema.Properties[$"Documents[{index}].ExpiresOn"] = new OpenApiSchema { Type = JsonSchemaType.String, Format = "date" };
            }
            operation.RequestBody = new OpenApiRequestBody { Required = method == nameof(VehiclesController.Register),
                Content = new Dictionary<string, OpenApiMediaType> { ["multipart/form-data"] = new() { Schema = schema } } };
            operation.Description += " El formulario interactivo muestra una foto y dos documentos; la API permite más bloques con índices consecutivos.";
        }
        if (method is not (nameof(VehiclesController.PhotoContent) or nameof(VehiclesController.DocumentContent))) return;
        operation.Responses ??= [];
        var response = new OpenApiResponse { Description = "Contenido privado autorizado.", Content = new Dictionary<string, OpenApiMediaType>() };
        foreach (var mime in method == nameof(VehiclesController.PhotoContent) ? new[] { "image/jpeg", "image/png" } : new[] { "application/pdf", "image/jpeg", "image/png" })
            response.Content[mime] = new OpenApiMediaType { Schema = new OpenApiSchema { Type = JsonSchemaType.String, Format = "binary" } };
        operation.Responses["200"] = response;
        operation.Responses["302"] = new OpenApiResponse { Description = "Redirección autorizada a URL S3 firmada temporal." };
    }
}
