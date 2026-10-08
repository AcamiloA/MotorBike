namespace UniversityParking.Domain.Common;

internal static class Guard
{
    internal static string Text(string? value, int maximumLength, string field)
    {
        var normalized = value?.Trim();
        if (string.IsNullOrEmpty(normalized) || normalized.Length > maximumLength)
            throw new DomainException("VALIDATION_ERROR", $"El campo {field} es obligatorio y admite hasta {maximumLength} caracteres.");
        return normalized;
    }

    internal static string? OptionalText(string? value, int maximumLength, string field) =>
        string.IsNullOrWhiteSpace(value) ? null : Text(value, maximumLength, field);

    internal static Guid Id(Guid value, string field)
    {
        if (value == Guid.Empty)
            throw new DomainException("VALIDATION_ERROR", $"El identificador {field} es obligatorio.");
        return value;
    }

    internal static Guid? OptionalId(Guid? value, string field) => value.HasValue ? Id(value.Value, field) : null;

    internal static T Defined<T>(T value) where T : struct, Enum
    {
        if (!Enum.IsDefined(value))
            throw new DomainException("VALIDATION_ERROR", "El valor seleccionado no es válido.");
        return value;
    }

    internal static DateTimeOffset Utc(DateTimeOffset value)
    {
        if (value == default)
            throw new DomainException("VALIDATION_ERROR", "La fecha es obligatoria.");
        return value.ToUniversalTime();
    }

    internal static void Chronology(DateTimeOffset value, DateTimeOffset minimum)
    {
        if (value < minimum)
            throw new DomainException("VALIDATION_ERROR", "La fecha no puede ser anterior al inicio.");
    }
}
