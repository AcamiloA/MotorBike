using Microsoft.Extensions.Logging;
using UniversityParking.Application.Common.Abstractions;

namespace UniversityParking.Application.Files;

public sealed class UploadedFileBatch(IFileStorage storage, ILogger<UploadedFileBatch> logger) : IAsyncDisposable
{
    private readonly List<string> createdKeys = [];
    private bool committed;
    private bool commitStarted;
    public async Task<string> UploadAsync(Guid vehicleId, string category, ValidatedUpload file, CancellationToken cancellationToken)
        => await UploadKeyAsync($"vehicles/{vehicleId:D}/{category}/{Guid.NewGuid():N}{file.Extension}", file, cancellationToken);
    public Task<string> UploadIncidentAsync(Guid incidentId, ValidatedUpload file, CancellationToken cancellationToken) =>
        UploadKeyAsync($"incidents/{incidentId:D}/attachments/{Guid.NewGuid():N}{file.Extension}", file, cancellationToken);
    private async Task<string> UploadKeyAsync(string key, ValidatedUpload file, CancellationToken cancellationToken)
    {
        // Include uncertain uploads: the provider may have accepted the object before a transport failure.
        createdKeys.Add(key);
        await using var content = new MemoryStream(file.Bytes, writable: false);
        await storage.UploadAsync(new FileUpload(key, content, file.ContentType, file.Bytes.LongLength), cancellationToken);
        return key;
    }
    public void MarkCommitted() => committed = true;
    public void MarkCommitStarted() => commitStarted = true;
    public async ValueTask DisposeAsync()
    {
        if (committed) return;
        if (commitStarted)
        {
            // A transport failure during COMMIT cannot prove that PostgreSQL rolled back.
            // Keep evidence that may already be referenced by a committed transaction.
            logger.LogError("File compensation skipped because the transaction commit outcome is unknown.");
            return;
        }
        foreach (var key in createdKeys)
        {
            try { await storage.DeleteAsync(key, CancellationToken.None); }
            catch (Exception exception)
            {
                logger.LogError("File compensation failed. Key {StorageKey}. ExceptionType {ExceptionType}", key, exception.GetType().Name);
            }
        }
    }
}
