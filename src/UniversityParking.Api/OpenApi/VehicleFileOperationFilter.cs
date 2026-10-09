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
        if (method is nameof(VehiclesController.Register) or nameof(VehiclesController.Renew) or nameof(VehiclesController.UpdateVerificationImage))
        {
            operation.Description = method == nameof(VehiclesController.Renew)
                ? "Renovación sin nueva evidencia si el vehículo ya tiene VerificationImage; documentos legacy opcionales."
                : "Una sola VerificationImage.File JPEG/PNG hasta 5 MB. Tipo derivado: Licencia de Tránsito para carro/moto, foto para bicicleta/scooter. OCR configurable para carro/moto; nunca para bicicleta/scooter.";
            var schema = new OpenApiSchema { Type = JsonSchemaType.Object, Properties = new Dictionary<string, IOpenApiSchema>(), Required = new HashSet<string>() };
            if (method == nameof(VehiclesController.Register))
            {
                foreach (var name in new[] { "Type", "Plate", "FrameNumber", "Brand", "Model", "Color" })
                    schema.Properties[name] = new OpenApiSchema { Type = JsonSchemaType.String };
                foreach (var name in new[] { "Type", "Brand", "Model", "Color", "VerificationImage.File" }) schema.Required.Add(name);

                schema.Properties["VerificationImage.File"] = new OpenApiSchema { Type = JsonSchemaType.String, Format = "binary" };
            }
            if (method == nameof(VehiclesController.UpdateVerificationImage))
            { schema.Required.Add("VerificationImage.File"); schema.Properties["VerificationImage.File"] = new OpenApiSchema { Type = JsonSchemaType.String, Format = "binary" }; }
            if (method == nameof(VehiclesController.Renew)) for (var index = 0; index < 2; index++)
            {
                schema.Properties[$"Documents[{index}].Type"] = new OpenApiSchema { Type = JsonSchemaType.String,
                    Description = "VEHICLE_REGISTRATION, INSURANCE u OWNERSHIP_SUPPORT. Omitir el segundo bloque para bicicleta." };
                schema.Properties[$"Documents[{index}].File"] = new OpenApiSchema { Type = JsonSchemaType.String, Format = "binary" };
                schema.Properties[$"Documents[{index}].DocumentNumber"] = new OpenApiSchema { Type = JsonSchemaType.String };
                schema.Properties[$"Documents[{index}].IssuedOn"] = new OpenApiSchema { Type = JsonSchemaType.String, Format = "date" };
                schema.Properties[$"Documents[{index}].ExpiresOn"] = new OpenApiSchema { Type = JsonSchemaType.String, Format = "date" };
            }
            operation.RequestBody = new OpenApiRequestBody { Required = method != nameof(VehiclesController.Renew),
                Content = new Dictionary<string, OpenApiMediaType> { ["multipart/form-data"] = new() { Schema = schema } } };

        }
        if (method is not (nameof(VehiclesController.PhotoContent) or nameof(VehiclesController.VerificationContent) or nameof(VehiclesController.DocumentContent))) return;
        operation.Responses ??= [];
        var response = new OpenApiResponse { Description = "Contenido privado autorizado.", Content = new Dictionary<string, OpenApiMediaType>() };
        foreach (var mime in method != nameof(VehiclesController.DocumentContent) ? new[] { "image/jpeg", "image/png" } : new[] { "application/pdf", "image/jpeg", "image/png" })
            response.Content[mime] = new OpenApiMediaType { Schema = new OpenApiSchema { Type = JsonSchemaType.String, Format = "binary" } };
        operation.Responses["200"] = response;
        operation.Responses["302"] = new OpenApiResponse { Description = "Redirección autorizada a URL S3 firmada temporal." };
    }
}
