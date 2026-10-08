using UniversityParking.Application.Common.Results;

namespace UniversityParking.Application.Common.Errors;

public static class UniversityErrors
{
    public static Error NotFound { get; } = new("UNIVERSITY_NOT_FOUND", "La universidad seleccionada no existe.", ErrorType.Validation);
    public static Error Inactive { get; } = new("UNIVERSITY_INACTIVE", "Selecciona una universidad activa.", ErrorType.Validation);
}
