using Amazon;
using Amazon.Runtime;
using Amazon.Textract;
using Amazon.Textract.Model;
using Microsoft.Extensions.Options;
using UniversityParking.Infrastructure.Documents;

namespace UniversityParking.Infrastructure.Tests.Documents;

public sealed class TextractExtractorTests
{
    private sealed class Client : AmazonTextractClient
    {
        public int Calls; public byte[]? Bytes; public bool HasS3; public CancellationToken Token;
        public Func<CancellationToken, Task<DetectDocumentTextResponse>> Reply = _ => Task.FromResult(new DetectDocumentTextResponse());
        public Client() : base(new BasicAWSCredentials("TEST-ONLY", "TEST-ONLY"), new AmazonTextractConfig { RegionEndpoint = RegionEndpoint.USEast1 }) { }
        public override Task<DetectDocumentTextResponse> DetectDocumentTextAsync(DetectDocumentTextRequest request, CancellationToken token = default)
        { Calls++; Bytes = request.Document.Bytes.ToArray(); HasS3 = request.Document.S3Object is not null; Token = token; return Reply(token); }
    }
    [Fact]
    public async Task MapsLinesWordsConfidenceAndGeometryFromSingleBytesRequest()
    {
        using var client = new Client { Reply = _ => Task.FromResult(new DetectDocumentTextResponse { Blocks =
            [new() { BlockType = BlockType.LINE, Text = "synthetic-line", Confidence = 99, Geometry = new() { BoundingBox = new() { Left = .1f, Top = .2f, Width = .3f, Height = .4f } } },
             new() { BlockType = BlockType.WORD, Text = "synthetic-word", Confidence = 88 }, new() { BlockType = BlockType.PAGE }] }) };
        var result = await new AwsTextractDocumentTextExtractor(() => client, Options.Create(new TextractOptions())).ExtractAsync([1, 2], "image/png", default);
        Assert.True(result.IsSuccess); var line = Assert.Single(result.Value.Lines); Assert.Equal("synthetic-line", line.Text); Assert.Equal(99, line.Confidence);
        Assert.Equal(.1, line.BoundingBox!.Left, 6); Assert.Equal("synthetic-word", Assert.Single(result.Value.Words).Text);
        Assert.Equal(new byte[] { 1, 2 }, client.Bytes); Assert.False(client.HasS3); Assert.Equal(1, client.Calls);
    }
    [Fact]
    public async Task EmptyResponseIsEmptyExtractionNotTechnicalFailure()
    {
        using var client = new Client(); var result = await new AwsTextractDocumentTextExtractor(() => client, Options.Create(new TextractOptions())).ExtractAsync([1], "image/jpeg", default);
        Assert.True(result.IsSuccess); Assert.Empty(result.Value.Lines); Assert.Empty(result.Value.Words);
    }
    [Fact]
    public async Task ExceptionAndMissingCredentialsAreSanitized()
    {
        using var client = new Client { Reply = _ => throw new AmazonTextractException("private-owner-secret") };
        var extractor = new AwsTextractDocumentTextExtractor(() => client, Options.Create(new TextractOptions()));
        var result = await extractor.ExtractAsync([1], "image/png", default); Assert.Equal("DOCUMENT_OCR_UNAVAILABLE", result.Error!.Code);
        Assert.DoesNotContain("private", result.Error.Message);
        result = await new AwsTextractDocumentTextExtractor(() => throw new InvalidOperationException("credentials"), Options.Create(new TextractOptions())).ExtractAsync([1], "image/png", default);
        Assert.Equal("DOCUMENT_OCR_UNAVAILABLE", result.Error!.Code);
    }
    [Fact]
    public async Task PreCanceledRequestDoesNotResolveClient()
    {
        using var source = new CancellationTokenSource(); source.Cancel(); var calls = 0;
        var extractor = new AwsTextractDocumentTextExtractor(() => { calls++; throw new InvalidOperationException(); }, Options.Create(new TextractOptions()));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => extractor.ExtractAsync([1], "image/png", source.Token)); Assert.Equal(0, calls);
    }
    [Fact]
    public async Task TimeoutMapsToUnavailableAndCancelsSdkToken()
    {
        using var client = new Client { Reply = async token => { await Task.Delay(Timeout.Infinite, token); return new(); } };
        var result = await new AwsTextractDocumentTextExtractor(() => client, Options.Create(new TextractOptions { TimeoutSeconds = 1 })).ExtractAsync([1], "image/png", default);
        Assert.Equal("DOCUMENT_OCR_UNAVAILABLE", result.Error!.Code); Assert.True(client.Token.IsCancellationRequested); Assert.Equal(1, client.Calls);
    }
    [Fact]
    public async Task UnsupportedInputDoesNotResolveSdk()
    {
        var extractor = new AwsTextractDocumentTextExtractor(() => throw new InvalidOperationException("should not resolve"), Options.Create(new TextractOptions()));
        Assert.Equal("DOCUMENT_OCR_UNAVAILABLE", (await extractor.ExtractAsync([1], "application/pdf", default)).Error!.Code);
    }
}
