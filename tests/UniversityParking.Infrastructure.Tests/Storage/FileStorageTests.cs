using Amazon.S3;
using Amazon.Runtime;
using UniversityParking.Application.Common.Abstractions;
using UniversityParking.Infrastructure.Storage;

namespace UniversityParking.Infrastructure.Tests.Storage;

public sealed class FileStorageTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "UniversityParking-storage-" + Guid.NewGuid().ToString("N"));
    private LocalFileStorage Storage => new(new StorageOptions { LocalRootPath = root });
    [Fact]
    public async Task LocalStorageUploadsReadsAndDeletesPrivateFile()
    {
        const string key = "vehicles/123/photos/generated.png";
        var storage = Storage;
        await using var input = new MemoryStream([1, 2, 3, 4]);
        await storage.UploadAsync(new(key, input, "image/png", 4), default);
        Assert.True(input.CanRead);
        await using (var content = await storage.OpenReadAsync(key, default))
        {
            using var buffer = new MemoryStream();
            await content.CopyToAsync(buffer);
            Assert.Equal(new byte[] { 1, 2, 3, 4 }, buffer.ToArray());
        }
        Assert.Null(await storage.GetReadUrlAsync(key, default));
        await storage.DeleteAsync(key, default);
        Assert.False(File.Exists(Path.Combine(root, key)));
    }
    [Theory]
    [InlineData("../escape.png")]
    [InlineData("/absolute.png")]
    [InlineData("C:/escape.png")]
    [InlineData("vehicles/../../escape.png")]
    [InlineData("vehicles\\escape.png")]
    [InlineData("vehicles//escape.png")]
    public async Task UnsafeKeysAreRejected(string key)
    {
        var storage = Storage;
        await Assert.ThrowsAsync<ArgumentException>(() => storage.OpenReadAsync(key, default));
        await Assert.ThrowsAsync<ArgumentException>(() => storage.DeleteAsync(key, default));
    }
    [Fact]
    public async Task InvalidLengthLeavesNoFileOrTemporaryUpload()
    {
        var storage = Storage;
        await using var stream = new MemoryStream([1, 2, 3]);
        await Assert.ThrowsAsync<InvalidDataException>(() => storage.UploadAsync(new("vehicles/v/file.png", stream, "image/png", 2), default));
        Assert.Empty(Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories));
    }
    [Fact]
    public void PublicWwwrootIsRejected() => Assert.Throws<ArgumentException>(() =>
        new LocalFileStorage(new StorageOptions { LocalRootPath = Path.Combine(root, "wwwroot") }));
    [Fact]
    public async Task ExistingEvidenceCannotBeOverwritten()
    {
        var storage = Storage;
        await using var original = new MemoryStream([1]);
        await storage.UploadAsync(new("vehicles/file.png", original, "image/png", 1), default);
        await using var replacement = new MemoryStream([2]);
        await Assert.ThrowsAsync<IOException>(() => storage.UploadAsync(new("vehicles/file.png", replacement, "image/png", 1), default));
        Assert.Equal(new byte[] { 1 }, await File.ReadAllBytesAsync(Path.Combine(root, "vehicles/file.png")));
    }
    [Fact]
    public async Task S3SigningUsesPrivateTemporaryUrlWithoutNetworkOrRealCredentials()
    {
        using var client = new AmazonS3Client(new BasicAWSCredentials("TEST-ONLY-ACCESS", "TEST-ONLY-SECRET"),
            new AmazonS3Config { ServiceURL = "https://s3.example.test", AuthenticationRegion = "us-east-1", ForcePathStyle = true });
        var storage = new S3CompatibleFileStorage(client, new StorageOptions { Bucket = "private-test-bucket", SignedUrlExpirationMinutes = 5 }, new CurrentClock());
        var url = await storage.GetReadUrlAsync("vehicles/v/photos/a.png", default);
        Assert.Equal("https", url!.Scheme);
        Assert.Contains("X-Amz-Signature=", url.Query);
        Assert.Contains("X-Amz-Expires=300", url.Query);
        Assert.Contains("private-test-bucket", url.AbsolutePath);
    }
    public void Dispose()
    {
        // The target is the unique temporary directory created by this test instance.
        if (!Path.GetFullPath(root).StartsWith(Path.Combine(Path.GetTempPath(), "UniversityParking-storage-"), StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Unexpected test storage directory.");
        if (Directory.Exists(root)) Directory.Delete(root, true);
    }
    private sealed class CurrentClock : IClock { public DateTimeOffset UtcNow => DateTimeOffset.UtcNow; }
}
