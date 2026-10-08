namespace UniversityParking.Application.Common.Abstractions;

public sealed record FileUpload(string StorageKey, Stream Content, string ContentType, long SizeBytes);
public sealed record StoredFile(string StorageKey, string ContentType, long SizeBytes);

public interface IFileStorage
{
    // The caller owns the upload stream and disposes streams returned by OpenReadAsync.
    Task<StoredFile> UploadAsync(FileUpload upload, CancellationToken cancellationToken);
    Task<Stream> OpenReadAsync(string storageKey, CancellationToken cancellationToken);
    Task DeleteAsync(string storageKey, CancellationToken cancellationToken);
    // Local storage returns null; the API provides its authorized content endpoint.
    Task<Uri?> GetReadUrlAsync(string storageKey, CancellationToken cancellationToken);
}
