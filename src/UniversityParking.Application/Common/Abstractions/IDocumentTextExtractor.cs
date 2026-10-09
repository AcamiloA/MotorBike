using UniversityParking.Application.Common.Results;

namespace UniversityParking.Application.Common.Abstractions;

public interface IDocumentTextExtractor
{
    Task<Result<DocumentTextExtractionResult>> ExtractAsync(byte[] image, string contentType, CancellationToken cancellationToken);
}
public sealed record DocumentBoundingBox(double Left, double Top, double Width, double Height);
public sealed record DocumentTextItem(string Text, double Confidence, DocumentBoundingBox? BoundingBox = null)
{
    public override string ToString() => "DocumentTextItem [redacted]";
}
public sealed record DocumentTextExtractionResult(IReadOnlyList<DocumentTextItem> Lines, IReadOnlyList<DocumentTextItem> Words)
{
    public override string ToString() => $"DocumentTextExtractionResult {{ Lines = {Lines.Count}, Words = {Words.Count} }}";
}
