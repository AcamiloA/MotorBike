using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using UniversityParking.Application.Common.Results;

namespace UniversityParking.Api.ExceptionHandling;

public static class ProblemResponses
{
    public const string InternalServerErrorCode = "INTERNAL_SERVER_ERROR";

    public static ProblemDetails Create(HttpContext context, int status, string code, string detail,
        IReadOnlyDictionary<string, IReadOnlyList<string>>? errors = null)
    {
        var problem = new ProblemDetails
        {
            Type = "about:blank", Title = ReasonPhrases.GetReasonPhrase(status), Status = status,
            Detail = detail, Instance = context.Request.Path
        };
        problem.Extensions["code"] = code;
        problem.Extensions["traceId"] = context.TraceIdentifier;
        if (errors is { Count: > 0 })
            problem.Extensions["errors"] = errors.GroupBy(pair => FieldName(pair.Key))
                .ToDictionary(group => group.Key, group => group.SelectMany(pair => pair.Value).Distinct().ToArray());
        return problem;
    }

    public static ObjectResult Map(HttpContext context, Error error)
    {
        var status = error.Type switch
        {
            ErrorType.Validation => 400, ErrorType.Unauthorized => 401, ErrorType.Forbidden => 403,
            ErrorType.NotFound => 404, ErrorType.Conflict => 409, ErrorType.Unavailable => 503, _ => 500
        };
        var problem = Create(context, status, status == 500 ? InternalServerErrorCode : error.Code,
            status == 500 ? "Ocurrió un error inesperado." : error.Message, status == 500 ? null : error.ValidationErrors);
        var response = new ObjectResult(problem) { StatusCode = status };
        response.ContentTypes.Add("application/problem+json");
        return response;
    }

    public static Task WriteAsync(HttpContext context, int status, string code, string detail,
        IReadOnlyDictionary<string, IReadOnlyList<string>>? errors = null) =>
        Results.Problem(Create(context, status, code, detail, errors)).ExecuteAsync(context);

    public static string FieldName(string value)
    {
        if (string.IsNullOrEmpty(value) || value == "$") return "request";
        return string.Join('.', value.TrimStart('$', '.').Split('.').Select(JsonNamingPolicy.CamelCase.ConvertName));
    }

    public static Task WriteStatusAsync(HttpContext context)
    {
        var status = context.Response.StatusCode;
        var code = status switch
        {
            400 => "VALIDATION_ERROR", 401 => "AUTH_INVALID_CREDENTIALS", 403 => "FORBIDDEN",
            404 => "RESOURCE_NOT_FOUND", 405 => "METHOD_NOT_ALLOWED", 413 => "FILE_TOO_LARGE",
            415 => "UNSUPPORTED_MEDIA_TYPE", 429 => "RATE_LIMIT_EXCEEDED", 503 => "SERVICE_UNAVAILABLE",
            _ => InternalServerErrorCode
        };
        return WriteAsync(context, status, code, status >= 500 ? "El servicio no está disponible." : "No fue posible completar la solicitud.");
    }
}
