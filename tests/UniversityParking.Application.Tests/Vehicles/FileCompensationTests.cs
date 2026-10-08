using Microsoft.Extensions.Logging.Abstractions;
using UniversityParking.Application.Common.Abstractions;
using UniversityParking.Application.Files;

namespace UniversityParking.Application.Tests.Vehicles;

public sealed class FileCompensationTests
{
    [Fact]
    public async Task FailedOperationRemovesOnlyNewUploads()
    {
        var storage = new RecordingStorage();
        storage.Keys.Add("historical.png");
        await using (var batch = new UploadedFileBatch(storage, NullLogger<UploadedFileBatch>.Instance))
            await batch.UploadAsync(Guid.NewGuid(), "photos", new("original.png", "image/png", ".png", [1]), default);
        Assert.Equal(new[] { "historical.png" }, storage.Keys);
    }
    [Fact]
    public async Task CommittedOperationKeepsUploadedEvidence()
    {
        var storage = new RecordingStorage();
        await using (var batch = new UploadedFileBatch(storage, NullLogger<UploadedFileBatch>.Instance))
        {
            await batch.UploadAsync(Guid.NewGuid(), "photos", new("original.png", "image/png", ".png", [1]), default);
            batch.MarkCommitted();
        }
        Assert.Single(storage.Keys);
    }
    [Fact]
    public async Task UncertainCommitDoesNotDeletePossiblyCommittedEvidence()
    {
        var storage = new RecordingStorage();
        await using (var batch = new UploadedFileBatch(storage, NullLogger<UploadedFileBatch>.Instance))
        {
            await batch.UploadAsync(Guid.NewGuid(), "photos", new("original.png", "image/png", ".png", [1]), default);
            batch.MarkCommitStarted();
        }
        Assert.Single(storage.Keys);
    }
    [Fact]
    public async Task CleanupFailureDoesNotReplaceOriginalFailure()
    {
        var storage = new RecordingStorage { FailDelete = true };
        var failure = new IOException("Original failure");
        var observed = await Assert.ThrowsAsync<IOException>(async () =>
        {
            await using var batch = new UploadedFileBatch(storage, NullLogger<UploadedFileBatch>.Instance);
            await batch.UploadAsync(Guid.NewGuid(), "photos", new("original.png", "image/png", ".png", [1]), default);
            throw failure;
        });
        Assert.Same(failure, observed);
    }
    private sealed class RecordingStorage : IFileStorage
    {
        public List<string> Keys { get; } = [];
        public bool FailDelete { get; init; }
        public Task<StoredFile> UploadAsync(FileUpload upload, CancellationToken cancellationToken)
        { Keys.Add(upload.StorageKey); return Task.FromResult(new StoredFile(upload.StorageKey, upload.ContentType, upload.SizeBytes)); }
        public Task DeleteAsync(string key, CancellationToken cancellationToken)
        { if (FailDelete) throw new IOException("Cleanup failure"); Keys.Remove(key); return Task.CompletedTask; }
        public Task<Stream> OpenReadAsync(string key, CancellationToken cancellationToken) => Task.FromResult<Stream>(new MemoryStream());
        public Task<Uri?> GetReadUrlAsync(string key, CancellationToken cancellationToken) => Task.FromResult<Uri?>(null);
    }
}
