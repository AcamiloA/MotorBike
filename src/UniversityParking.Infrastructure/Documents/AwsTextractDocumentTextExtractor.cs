using Amazon;
using Amazon.Textract;
using Amazon.Textract.Model;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using UniversityParking.Application.Common.Abstractions;
using UniversityParking.Application.Common.Results;
using UniversityParking.Application.Documents;

namespace UniversityParking.Infrastructure.Documents;

public sealed class TextractOptions
{
    public string Region { get; set; } = "us-east-1";
    public int TimeoutSeconds { get; set; } = 15;
}
public static class TextractRegistration
{
    public static IServiceCollection AddDocumentOcr(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<TextractOptions>().Bind(configuration.GetSection("Textract"), o => o.ErrorOnUnknownConfiguration = true)
            .Validate(x => RegionEndpoint.EnumerableAllRegions.Any(r => r.SystemName == x.Region), "Textract:Region no es válida.")
            .Validate(x => x.TimeoutSeconds is >= 1 and <= 60, "Textract:TimeoutSeconds debe estar entre 1 y 60.").ValidateOnStart();
        services.AddSingleton<IAmazonTextract>(provider =>
        {
            var options = provider.GetRequiredService<IOptions<TextractOptions>>().Value;
            // Standard SDK credential chain; independent of S3 credentials. Constructed on first OCR only.
            return new AmazonTextractClient(new AmazonTextractConfig
            { RegionEndpoint = RegionEndpoint.GetBySystemName(options.Region), Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds), MaxErrorRetry = 1, LogResponse = false });
        });
        services.AddSingleton<Func<IAmazonTextract>>(provider => () => provider.GetRequiredService<IAmazonTextract>());
        services.AddSingleton<IDocumentTextExtractor, AwsTextractDocumentTextExtractor>();
        return services;
    }
}
public sealed class AwsTextractDocumentTextExtractor(Func<IAmazonTextract> clientFactory, IOptions<TextractOptions> options)
    : IDocumentTextExtractor
{
    public async Task<Result<DocumentTextExtractionResult>> ExtractAsync(byte[] image, string contentType, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (image.Length is 0 or > 5242880 || contentType is not ("image/jpeg" or "image/png"))
            return Result<DocumentTextExtractionResult>.Failure(TransitLicenseErrors.Unavailable);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(options.Value.TimeoutSeconds));
        try
        {
            var client = await Task.Run(clientFactory, timeout.Token).WaitAsync(timeout.Token);
            using var bytes = new MemoryStream(image, writable: false);
            var response = await client.DetectDocumentTextAsync(new() { Document = new Document { Bytes = bytes } }, timeout.Token)
                .WaitAsync(timeout.Token);
            cancellationToken.ThrowIfCancellationRequested();
            var blocks = response.Blocks ?? [];
            DocumentTextItem Map(Block block)
            {
                var box = block.Geometry?.BoundingBox;
                return new(block.Text ?? "", Convert.ToDouble(block.Confidence), box is null ? null : new(
                    Convert.ToDouble(box.Left), Convert.ToDouble(box.Top), Convert.ToDouble(box.Width), Convert.ToDouble(box.Height)));
            }
            return Result<DocumentTextExtractionResult>.Success(new(
                blocks.Where(x => x.BlockType == BlockType.LINE).Select(Map).ToArray(),
                blocks.Where(x => x.BlockType == BlockType.WORD).Select(Map).ToArray()));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (Exception) { return Result<DocumentTextExtractionResult>.Failure(TransitLicenseErrors.Unavailable); }
    }
}
