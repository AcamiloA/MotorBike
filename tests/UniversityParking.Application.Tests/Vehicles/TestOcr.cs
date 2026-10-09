using UniversityParking.Application.Common.Abstractions;
using UniversityParking.Application.Common.Results;
using UniversityParking.Application.Documents;

namespace UniversityParking.Application.Tests.Vehicles;

internal sealed class TestOcr : IDocumentTextExtractor
{
    public int Calls { get; private set; }
    public DocumentTextExtractionResult Text { get; set; } = ValidText();
    public bool Unavailable { get; set; }
    public static DocumentTextExtractionResult ValidText(double confidence = 99) => new(new[]
    { "REPÚBLICA DE COLOMBIA", "MINISTERIO DE TRANSPORTE", "LICENCIA DE TRÁNSITO", "PLACA OCR999", "MARCA", "LÍNEA", "MODELO", "COLOR", "SERVICIO", "COMBUSTIBLE" }
        .Select(x => new DocumentTextItem(x, confidence)).ToArray(), []);
    public Task<Result<DocumentTextExtractionResult>> ExtractAsync(byte[] image, string mime, CancellationToken token)
    { token.ThrowIfCancellationRequested(); Calls++; return Task.FromResult(Unavailable ? Result<DocumentTextExtractionResult>.Failure(TransitLicenseErrors.Unavailable) : Result<DocumentTextExtractionResult>.Success(Text)); }
    public static ITransitLicenseValidationService Service() => new TransitLicenseValidationService(new TestOcr(), new TransitLicenseFormatValidator());
}
