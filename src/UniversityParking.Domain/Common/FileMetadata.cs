namespace UniversityParking.Domain.Common;

internal static class FileMetadata
{
    internal static string StorageKey(string value)
    {
        var key = Guard.Text(value, 500, "clave de archivo");
        if (key.StartsWith('/') || key.Contains('\\') || key.Contains(':') ||
            key.Split('/').Any(segment => segment is ".." or "." or ""))
            throw new DomainException("VALIDATION_ERROR", "La clave de archivo no es válida.");
        return key;
    }

    internal static string ContentType(string value, bool allowPdf)
    {
        var type = Guard.Text(value, 100, "tipo de archivo");
        if (type is not ("image/jpeg" or "image/png") && !(allowPdf && type == "application/pdf"))
            throw new DomainException("FILE_TYPE_NOT_ALLOWED", "El tipo de archivo no está permitido.");
        return type;
    }

    internal static long Size(long value, long maximum)
    {
        if (value <= 0)
            throw new DomainException("VALIDATION_ERROR", "El archivo está vacío.");
        if (value > maximum)
            throw new DomainException("FILE_TOO_LARGE", "El archivo supera el tamaño permitido.");
        return value;
    }
}
