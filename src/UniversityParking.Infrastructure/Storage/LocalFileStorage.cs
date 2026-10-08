using UniversityParking.Application.Common.Abstractions;

namespace UniversityParking.Infrastructure.Storage;

public sealed class LocalFileStorage : IFileStorage
{
    private readonly string root;
    public LocalFileStorage(StorageOptions options)
    {
        root = Path.GetFullPath(options.LocalRootPath);
        if (root.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            .Any(x => string.Equals(x, "wwwroot", StringComparison.OrdinalIgnoreCase)))
            throw new ArgumentException("Los archivos deben almacenarse fuera de wwwroot.");
        CheckLinks(root);
    }
    public async Task<StoredFile> UploadAsync(FileUpload upload, CancellationToken cancellationToken)
    {
        var path = Resolve(upload.StorageKey);
        if (upload.SizeBytes <= 0 || upload.SizeBytes > 10 * 1024 * 1024) throw new ArgumentException("Tamaño de archivo inválido.");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        CheckLinks(Path.GetDirectoryName(path)!);
        var temporary = path + "." + Guid.NewGuid().ToString("N") + ".upload";
        try
        {
            await using (var target = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, FileOptions.Asynchronous))
            {
                var buffer = new byte[81920];
                long copied = 0;
                int count;
                while ((count = await upload.Content.ReadAsync(buffer, cancellationToken)) > 0)
                {
                    copied += count;
                    if (copied > upload.SizeBytes) throw new InvalidDataException("El tamaño del archivo no coincide.");
                    await target.WriteAsync(buffer.AsMemory(0, count), cancellationToken);
                }
                if (copied != upload.SizeBytes) throw new InvalidDataException("El tamaño del archivo no coincide.");
            }
            File.Move(temporary, path, overwrite: false);
            return new StoredFile(upload.StorageKey, upload.ContentType, upload.SizeBytes);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
    public Task<Stream> OpenReadAsync(string storageKey, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Stream content = new FileStream(Resolve(storageKey), FileMode.Open, FileAccess.Read, FileShare.Read, 81920, FileOptions.Asynchronous);
        return Task.FromResult(content);
    }
    public Task DeleteAsync(string storageKey, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        File.Delete(Resolve(storageKey));
        return Task.CompletedTask;
    }
    public Task<Uri?> GetReadUrlAsync(string storageKey, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Resolve(storageKey);
        return Task.FromResult<Uri?>(null);
    }
    private string Resolve(string key)
    {
        StorageKeys.Validate(key);
        var path = Path.GetFullPath(Path.Combine(root, key.Replace('/', Path.DirectorySeparatorChar)));
        if (!path.StartsWith(root.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("La clave está fuera del almacenamiento privado.");
        CheckLinks(path);
        return path;
    }
    private static void CheckLinks(string path)
    {
        var current = path;
        while (!string.IsNullOrEmpty(current))
        {
            if ((File.Exists(current) || Directory.Exists(current)) && (File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                throw new IOException("No se permiten enlaces en el almacenamiento privado.");
            current = Path.GetDirectoryName(current);
        }
    }
}
