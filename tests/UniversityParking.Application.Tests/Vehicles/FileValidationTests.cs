using UniversityParking.Application.Files;

namespace UniversityParking.Application.Tests.Vehicles;

public sealed class FileValidationTests
{
    public static byte[] Png => [137, 80, 78, 71, 13, 10, 26, 10, 1, 2, 3];
    [Theory]
    [InlineData(".png", "image/png", true)]
    [InlineData(".jpg", "image/jpeg", true)]
    [InlineData(".jpeg", "image/jpeg", true)]
    [InlineData(".pdf", "application/pdf", false)]
    public async Task SupportedSignaturesAreAccepted(string extension, string mime, bool photo)
    {
        byte[] bytes = mime == "image/png" ? Png : mime == "image/jpeg" ? [255, 216, 255, 1] : "%PDF-1.7"u8.ToArray();
        var result = await new FileUploadValidator().ValidateAsync(new("../../original" + extension, mime, bytes.Length,
            () => new MemoryStream(bytes)), photo, default);
        Assert.True(result.IsSuccess);
        Assert.Equal("original" + extension, result.Value.FileName);
    }
    [Theory]
    [InlineData(".exe", "image/png", true)]
    [InlineData(".png", "application/pdf", true)]
    [InlineData(".pdf", "application/pdf", true)]
    [InlineData(".html", "text/html", false)]
    public async Task WrongExtensionOrMimeIsRejected(string extension, string mime, bool photo)
    {
        var result = await new FileUploadValidator().ValidateAsync(new("file" + extension, mime, Png.Length,
            () => new MemoryStream(Png)), photo, default);
        Assert.Equal("FILE_TYPE_NOT_ALLOWED", result.Error!.Code);
    }
    [Fact]
    public async Task SpoofedMagicBytesAreRejected()
    {
        byte[] bytes = "<script>attack</script>"u8.ToArray();
        var result = await new FileUploadValidator().ValidateAsync(new("image.png", "image/png", bytes.Length,
            () => new MemoryStream(bytes)), true, default);
        Assert.Equal("FILE_TYPE_NOT_ALLOWED", result.Error!.Code);
    }
    [Theory]
    [InlineData(true, 5242881)]
    [InlineData(false, 10485761)]
    public async Task DeclaredOversizeIsRejectedWithoutReading(bool photo, long size)
    {
        var opened = false;
        var result = await new FileUploadValidator().ValidateAsync(new("file.png", "image/png", size,
            () => { opened = true; return new MemoryStream(); }), photo, default);
        Assert.Equal("FILE_TOO_LARGE", result.Error!.Code);
        Assert.False(opened);
    }
    [Fact]
    public async Task ActualOversizeCannotBypassDeclaredLength()
    {
        var bytes = new byte[5 * 1024 * 1024 + 1];
        Png.CopyTo(bytes, 0);
        var result = await new FileUploadValidator().ValidateAsync(new("file.png", "image/png", 1,
            () => new MemoryStream(bytes)), true, default);
        Assert.Equal("FILE_TOO_LARGE", result.Error!.Code);
    }
    [Fact]
    public async Task MismatchedLengthIsRejected()
    {
        var result = await new FileUploadValidator().ValidateAsync(new("file.png", "image/png", Png.Length + 1,
            () => new MemoryStream(Png)), true, default);
        Assert.True(result.IsFailure);
    }
}
