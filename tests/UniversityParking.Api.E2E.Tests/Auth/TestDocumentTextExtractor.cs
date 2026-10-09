using UniversityParking.Application.Common.Abstractions;
using UniversityParking.Application.Common.Results;
using UniversityParking.Application.Documents;

namespace UniversityParking.Api.E2E.Tests.Auth;

// Test-only synthetic text. No AWS client/credentials/network; every E2E factory replaces the production extractor.
public sealed class TestDocumentTextExtractor : IDocumentTextExtractor
{
    public int Calls { get; private set; }
    public string[] Lines { get; set; } = ["REPUBLICA DE COLOMBIA", "MINISTERIO DE TRANSPORTE", "LICENCIA DE TRANSITO", "PLACA OCR999", "MARCA", "LINEA", "MODELO", "COLOR", "SERVICIO", "COMBUSTIBLE"];
    public double Confidence { get; set; } = 99;
    public bool Fail { get; set; }
    public Task<Result<DocumentTextExtractionResult>> ExtractAsync(byte[] image, string contentType, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested(); Calls++;
        return Task.FromResult(Fail ? Result<DocumentTextExtractionResult>.Failure(TransitLicenseErrors.Unavailable)
            : Result<DocumentTextExtractionResult>.Success(new(Lines.Select(x => new DocumentTextItem(x, Confidence)).ToArray(), [])));
    }
}
