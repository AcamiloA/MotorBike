using UniversityParking.Application.Common.Abstractions;
using UniversityParking.Application.Documents;
using UniversityParking.Application.Tests.Vehicles;

namespace UniversityParking.Application.Tests.Documents;

public sealed class TransitLicenseTests
{
    private readonly TransitLicenseFormatValidator validator = new();
    [Theory][InlineData("LICENCIA DE TRÁNSITO")][InlineData("licencia de transito")][InlineData(" L1CENCIA DE TRANSIT0 ")][InlineData("LICENCIA: DE\nTRÁNSITO")]
    public void ClearFormatToleratesAccentsPunctuationAndSingleCharacterErrors(string title)
    {
        var text = TestOcr.ValidText(); var lines = text.Lines.ToArray(); lines[2] = new(title, 99);
        Assert.Equal(TransitLicenseStatus.VALID, validator.Validate(text with { Lines = lines }).Status);
    }
    [Theory][InlineData(20, TransitLicenseStatus.UNREADABLE)][InlineData(60, TransitLicenseStatus.REVIEW_REQUIRED)][InlineData(99, TransitLicenseStatus.VALID)]
    public void QualityGateIsSeparateFromFormat(double confidence, TransitLicenseStatus expected)
        => Assert.Equal(expected, validator.Validate(TestOcr.ValidText(confidence)).Status);
    [Fact]
    public void EmptyAndTinyTextCannotBeValid()
    {
        Assert.Equal(TransitLicenseStatus.UNREADABLE, validator.Validate(new([], [])).Status);
        Assert.Equal(TransitLicenseStatus.UNREADABLE, validator.Validate(new([new("PLACA ABC123", 99)], [])).Status);
    }
    [Fact]
    public void UnrelatedLegibleDocumentIsInvalid()
        => Assert.Equal(TransitLicenseStatus.INVALID, validator.Validate(new([new("FACTURA SUPERMERCADO COMPRA PRODUCTOS PRECIO TOTAL PAGADO EFECTIVO", 99)], [])).Status);
    [Theory][InlineData(0)][InlineData(1)][InlineData(2)][InlineData(3)]
    public void MissingMandatoryAnchorRequiresReview(int missing)
    {
        var text = TestOcr.ValidText(); var lines = text.Lines.Where((_, index) => missing < 2 ? index != missing : index != missing).ToArray();
        if (missing < 2) lines = lines.Where(x => !x.Text.Contains("MINISTERIO") && !x.Text.Contains("REPÚBLICA")).ToArray();
        Assert.Equal(TransitLicenseStatus.REVIEW_REQUIRED, validator.Validate(text with { Lines = lines }).Status);
    }
    [Fact]
    public void OneInstitutionAndFiveFieldsAreEnough()
    {
        var text = TestOcr.ValidText();
        var lines = text.Lines.Take(9).Where(x => !x.Text.Contains("REPÚBLICA")).ToArray();
        var result = validator.Validate(text with { Lines = lines });
        Assert.Equal(TransitLicenseStatus.VALID, result.Status); Assert.Equal(5, result.CharacteristicFields);
    }
    [Fact]
    public void PartialTitleAndInsufficientDistinctFieldsCannotBeValid()
    {
        var text = TestOcr.ValidText(); var lines = text.Lines.Take(7).ToArray(); lines[2] = new("LICENCIA", 99);
        Assert.Equal(TransitLicenseStatus.REVIEW_REQUIRED, validator.Validate(text with { Lines = lines }).Status);
    }
    [Fact]
    public void DuplicateFieldsCannotInflateScore()
    {
        var lines = TestOcr.ValidText().Lines.Take(5).Concat(Enumerable.Repeat(new DocumentTextItem("MARCA", 99), 20)).ToArray();
        var result = validator.Validate(new(lines, [])); Assert.Equal(1, result.CharacteristicFields); Assert.NotEqual(TransitLicenseStatus.VALID, result.Status);
    }
    [Fact]
    public void ShortVinDoesNotMatchVinoAndSensitiveTextIsRedacted()
    {
        var result = validator.Validate(new([new("LICENCIA DE TRANSITO PLACA MINISTERIO DE TRANSPORTE MARCA MODELO COLOR SERVICIO VINO", 99)], []));
        Assert.Equal(4, result.CharacteristicFields); Assert.Equal(TransitLicenseStatus.REVIEW_REQUIRED, result.Status);
        Assert.DoesNotContain("private-owner", new DocumentTextItem("private-owner", 99).ToString());
    }
    [Fact]
    public async Task ServiceMapsTechnicalFailureWithoutDocumentClassification()
    {
        var ocr = new TestOcr { Unavailable = true };
        var result = await new TransitLicenseValidationService(ocr, validator, Microsoft.Extensions.Options.Options.Create(new DocumentOcrOptions { Enabled = true })).ValidateAsync(new("file.png", "image/png", ".png", [1]), default);
        Assert.Equal("DOCUMENT_OCR_UNAVAILABLE", result.Error!.Code);
    }
    [Fact]
    public async Task ServicePropagatesCallerCancellation()
    {
        using var source = new CancellationTokenSource(); source.Cancel(); var ocr = new TestOcr();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => new TransitLicenseValidationService(ocr, validator, Microsoft.Extensions.Options.Options.Create(new DocumentOcrOptions { Enabled = true })).ValidateAsync(new("file.png", "image/png", ".png", [1]), source.Token));
        Assert.Equal(0, ocr.Calls);
    }
}
