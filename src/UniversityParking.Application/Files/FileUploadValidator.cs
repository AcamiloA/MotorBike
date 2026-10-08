using UniversityParking.Application.Common.Errors;
using UniversityParking.Application.Common.Results;

namespace UniversityParking.Application.Files;

// A stream factory keeps HTTP types outside Application and makes ownership explicit.
public sealed record UploadSource(string FileName, string ContentType, long SizeBytes, Func<Stream> OpenRead);
public sealed record ValidatedUpload(string FileName, string ContentType, string Extension, byte[] Bytes);

public sealed class FileUploadValidator
{
    public async Task<Result<ValidatedUpload>> ValidateAsync(UploadSource source, bool photo, CancellationToken cancellationToken)
    {
        var maximum = (photo ? 5 : 10) * 1024 * 1024;
        if (source.SizeBytes > maximum) return Result<ValidatedUpload>.Failure(FileErrors.TooLarge);
        var name = Path.GetFileName((source.FileName ?? "").Replace('\\', '/')).Trim();
        var extension = Path.GetExtension(name).ToLowerInvariant();
        var mime = (source.ContentType ?? "").Trim().ToLowerInvariant();
        var valid = extension switch
        {
            ".jpg" or ".jpeg" => mime == "image/jpeg",
            ".png" => mime == "image/png",
            ".pdf" => !photo && mime == "application/pdf",
            _ => false
        };
        if (!valid || name.Length is 0 or > 255 || name.Any(char.IsControl) || source.SizeBytes <= 0)
            return Result<ValidatedUpload>.Failure(FileErrors.TypeNotAllowed);
        await using var input = source.OpenRead();
        using var buffer = new MemoryStream();
        var chunk = new byte[81920];
        int count;
        while ((count = await input.ReadAsync(chunk, cancellationToken)) > 0)
        {
            if (buffer.Length + count > maximum) return Result<ValidatedUpload>.Failure(FileErrors.TooLarge);
            await buffer.WriteAsync(chunk.AsMemory(0, count), cancellationToken);
        }
        var bytes = buffer.ToArray();
        if (bytes.LongLength != source.SizeBytes) return Result<ValidatedUpload>.Failure(FileErrors.TypeNotAllowed);
        var signature = mime switch
        {
            "image/png" => bytes.AsSpan().StartsWith(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }),
            "image/jpeg" => bytes.AsSpan().StartsWith(new byte[] { 255, 216, 255 }),
            "application/pdf" => bytes.AsSpan().StartsWith("%PDF-"u8),
            _ => false
        };
        return signature ? Result<ValidatedUpload>.Success(new(name, mime, extension, bytes)) :
            Result<ValidatedUpload>.Failure(FileErrors.TypeNotAllowed);
    }
}
