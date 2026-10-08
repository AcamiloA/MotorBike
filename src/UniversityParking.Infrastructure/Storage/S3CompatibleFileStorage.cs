using Amazon.S3;
using Amazon.S3.Model;
using UniversityParking.Application.Common.Abstractions;

namespace UniversityParking.Infrastructure.Storage;

public sealed class S3CompatibleFileStorage(IAmazonS3 client, StorageOptions options, IClock clock) : IFileStorage
{
    public async Task<StoredFile> UploadAsync(FileUpload upload, CancellationToken cancellationToken)
    {
        StorageKeys.Validate(upload.StorageKey);
        if (upload.SizeBytes <= 0 || upload.SizeBytes > 10 * 1024 * 1024 || !upload.Content.CanSeek ||
            upload.Content.Length - upload.Content.Position != upload.SizeBytes) throw new ArgumentException("Contenido de archivo inválido.");
        await client.PutObjectAsync(new PutObjectRequest
        {
            BucketName = options.Bucket, Key = upload.StorageKey, InputStream = upload.Content,
            ContentType = upload.ContentType, AutoCloseStream = false
        }, cancellationToken);
        return new StoredFile(upload.StorageKey, upload.ContentType, upload.SizeBytes);
    }
    public async Task<Stream> OpenReadAsync(string storageKey, CancellationToken cancellationToken)
    {
        StorageKeys.Validate(storageKey);
        var response = await client.GetObjectAsync(new GetObjectRequest { BucketName = options.Bucket, Key = storageKey }, cancellationToken);
        return new OwnedS3Stream(response);
    }
    public async Task DeleteAsync(string storageKey, CancellationToken cancellationToken)
    {
        StorageKeys.Validate(storageKey);
        await client.DeleteObjectAsync(new DeleteObjectRequest { BucketName = options.Bucket, Key = storageKey }, cancellationToken);
    }
    public async Task<Uri?> GetReadUrlAsync(string storageKey, CancellationToken cancellationToken)
    {
        StorageKeys.Validate(storageKey);
        cancellationToken.ThrowIfCancellationRequested();
        var url = await client.GetPreSignedURLAsync(new GetPreSignedUrlRequest
        {
            BucketName = options.Bucket, Key = storageKey, Verb = HttpVerb.GET,
            Expires = clock.UtcNow.AddMinutes(options.SignedUrlExpirationMinutes).UtcDateTime
        });
        return new Uri(url);
    }
    private sealed class OwnedS3Stream(GetObjectResponse response) : Stream
    {
        private Stream Inner => response.ResponseStream;
        public override bool CanRead => Inner.CanRead;
        public override bool CanSeek => Inner.CanSeek;
        public override bool CanWrite => false;
        public override long Length => Inner.Length;
        public override long Position { get => Inner.Position; set => Inner.Position = value; }
        public override int Read(byte[] buffer, int offset, int count) => Inner.Read(buffer, offset, count);
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) => Inner.ReadAsync(buffer, cancellationToken);
        public override long Seek(long offset, SeekOrigin origin) => Inner.Seek(offset, origin);
        public override void Flush() => Inner.Flush();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        protected override void Dispose(bool disposing) { if (disposing) response.Dispose(); base.Dispose(disposing); }
    }
}
