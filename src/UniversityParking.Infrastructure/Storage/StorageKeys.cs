namespace UniversityParking.Infrastructure.Storage;

internal static class StorageKeys
{
    public static void Validate(string key)
    {
        if (string.IsNullOrWhiteSpace(key) || key.Length > 500 || key.Split('/').Any(segment =>
            segment is "" or "." or ".." || segment.Any(c => !char.IsAsciiLetterOrDigit(c) && c is not ('-' or '_' or '.'))))
            throw new ArgumentException("La clave de almacenamiento no es válida.", nameof(key));
    }
}
