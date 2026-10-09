using System.Collections.ObjectModel;

namespace UniversityParking.Application.Common.Results;

public enum ErrorType { Validation, NotFound, Conflict, Forbidden, Unauthorized, Unexpected, Unavailable }

public sealed record Error
{
    public string Code { get; }
    public string Message { get; }
    public ErrorType Type { get; }
    public IReadOnlyDictionary<string, IReadOnlyList<string>> ValidationErrors { get; }

    public Error(string code, string message, ErrorType type,
        IReadOnlyDictionary<string, IReadOnlyList<string>>? validationErrors = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        if (!Enum.IsDefined(type)) throw new ArgumentOutOfRangeException(nameof(type));
        Code = code;
        Message = message;
        Type = type;
        ValidationErrors = new ReadOnlyDictionary<string, IReadOnlyList<string>>(
            validationErrors?.ToDictionary(pair => pair.Key,
                pair => (IReadOnlyList<string>)Array.AsReadOnly(pair.Value.ToArray())) ?? []);
    }
}
