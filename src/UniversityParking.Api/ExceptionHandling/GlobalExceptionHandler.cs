using Microsoft.AspNetCore.Diagnostics;
using UniversityParking.Domain.Common;

namespace UniversityParking.Api.ExceptionHandling;

public sealed class GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken cancellationToken)
    {
        if (exception is DomainException domain)
        {
            var status = domain.Code is "VALIDATION_ERROR" or "FILE_TOO_LARGE" or "FILE_TYPE_NOT_ALLOWED" ? 400 : 409;
            IReadOnlyDictionary<string, IReadOnlyList<string>>? fields = domain.Code == "VALIDATION_ERROR"
                ? new Dictionary<string, IReadOnlyList<string>> { ["request"] = new[] { domain.Message } } : null;
            await ProblemResponses.WriteAsync(context, status, domain.Code, domain.Message, fields);
        }
        else
        {
            // Log type and correlation without exception messages that could contain secrets.
            logger.LogError("Unexpected {ExceptionType} during request. TraceId {TraceId}. StackTrace {StackTrace}",
                exception.GetType().Name, context.TraceIdentifier, exception.StackTrace);
            await ProblemResponses.WriteAsync(context, 500, ProblemResponses.InternalServerErrorCode, "Ocurrió un error inesperado.");
        }
        return true;
    }
}
