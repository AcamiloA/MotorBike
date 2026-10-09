using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using UniversityParking.Application.Common.Abstractions;
using UniversityParking.Application.Common.Results;
using UniversityParking.Application.Files;

namespace UniversityParking.Application.Documents;

public enum TransitLicenseStatus { VALID, REVIEW_REQUIRED, INVALID, UNREADABLE }
public sealed record TransitLicenseFormatResult(TransitLicenseStatus Status, int Score, int CharacteristicFields,
    double AverageConfidence);
public interface ITransitLicenseFormatValidator
{
    TransitLicenseFormatResult Validate(DocumentTextExtractionResult extraction);
}
public interface ITransitLicenseValidationService
{
    Task<Result> ValidateAsync(ValidatedUpload upload, CancellationToken cancellationToken);
}
public static class TransitLicenseErrors
{
    public static Error Unavailable { get; } = new("DOCUMENT_OCR_UNAVAILABLE", "No fue posible validar el documento en este momento. Intenta nuevamente.", ErrorType.Unavailable);
    public static Error Review { get; } = new("TRANSIT_LICENSE_REVIEW_REQUIRED", "No pudimos validar completamente el formato. Intenta tomar una fotografía más clara.", ErrorType.Validation);
    public static Error Invalid { get; } = new("TRANSIT_LICENSE_INVALID_FORMAT", "La imagen no corresponde al formato esperado de una Licencia de Tránsito colombiana.", ErrorType.Validation);
    public static Error Unreadable { get; } = new("TRANSIT_LICENSE_UNREADABLE", "No pudimos leer el documento. Evita reflejos, acerca la tarjeta y toma nuevamente la fotografía.", ErrorType.Validation);
}
public sealed class TransitLicenseValidationService(IDocumentTextExtractor extractor, ITransitLicenseFormatValidator validator)
    : ITransitLicenseValidationService
{
    public async Task<Result> ValidateAsync(ValidatedUpload upload, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Result<DocumentTextExtractionResult> extraction;
        try { extraction = await extractor.ExtractAsync(upload.Bytes, upload.ContentType, cancellationToken); }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (Exception) { return Result.Failure(TransitLicenseErrors.Unavailable); }
        cancellationToken.ThrowIfCancellationRequested();
        if (extraction.IsFailure) return Result.Failure(TransitLicenseErrors.Unavailable);
        return validator.Validate(extraction.Value).Status switch
        {
            TransitLicenseStatus.VALID => Result.Success(),
            TransitLicenseStatus.REVIEW_REQUIRED => Result.Failure(TransitLicenseErrors.Review),
            TransitLicenseStatus.INVALID => Result.Failure(TransitLicenseErrors.Invalid),
            _ => Result.Failure(TransitLicenseErrors.Unreadable)
        };
    }
}
public static class TransitLicenseRules
{
    public const int TitleWeight = 35, PlateWeight = 15, InstitutionWeight = 15, FieldWeight = 5;
    public const int MinimumWords = 6, MinimumFields = 5, ValidScore = 90;
    public const double UnreadableConfidence = 35, ValidConfidence = 80, AnchorConfidence = 75;
}
public sealed class TransitLicenseFormatValidator : ITransitLicenseFormatValidator
{
    private static readonly string[] Fields = ["MARCA", "LINEA", "MODELO", "CILINDRADA", "COLOR", "SERVICIO", "CLASE DE VEHICULO",
        "TIPO DE CARROCERIA", "COMBUSTIBLE", "NUMERO DE MOTOR", "VIN", "NUMERO DE SERIE", "NUMERO DE CHASIS", "PROPIETARIO", "IDENTIFICACION"];
    public TransitLicenseFormatResult Validate(DocumentTextExtractionResult extraction)
    {
        var lines = extraction.Lines.Where(x => !string.IsNullOrWhiteSpace(x.Text)).ToArray();
        var words = extraction.Words.Where(x => !string.IsNullOrWhiteSpace(x.Text)).ToArray();
        var items = words.Length > 0 ? words : lines;
        var count = items.Sum(x => Tokens(x.Text).Length);
        var average = count == 0 ? 0 : items.Sum(x => Quality(x.Confidence) * Tokens(x.Text).Length) / count;
        if (count < TransitLicenseRules.MinimumWords || average < TransitLicenseRules.UnreadableConfidence)
            return new(TransitLicenseStatus.UNREADABLE, 0, 0, average);
        if (lines.Length == 0) lines = [new(string.Join(' ', words.Select(x => x.Text)), average)];
        var candidates = lines.Concat(lines.Zip(lines.Skip(1), (a, b) => new DocumentTextItem(a.Text + " " + b.Text,
            Math.Min(Quality(a.Confidence), Quality(b.Confidence))))).ToArray();
        double Match(string phrase) => candidates.Where(x => ContainsPhrase(Tokens(x.Text), Tokens(phrase)))
            .Select(x => Quality(x.Confidence)).DefaultIfEmpty(0).Max();
        var title = Match("LICENCIA DE TRANSITO"); var plate = Match("PLACA");
        var institutions = new[] { Match("REPUBLICA DE COLOMBIA"), Match("MINISTERIO DE TRANSPORTE") };
        var fields = Fields.Count(x => Match(x) >= TransitLicenseRules.AnchorConfidence);
        var score = Math.Min(100, (title >= TransitLicenseRules.AnchorConfidence ? TransitLicenseRules.TitleWeight : 0)
            + (plate >= TransitLicenseRules.AnchorConfidence ? TransitLicenseRules.PlateWeight : 0)
            + institutions.Count(x => x >= TransitLicenseRules.AnchorConfidence) * TransitLicenseRules.InstitutionWeight
            + Math.Min(6, fields) * TransitLicenseRules.FieldWeight);
        var valid = title >= TransitLicenseRules.AnchorConfidence && plate >= TransitLicenseRules.AnchorConfidence
            && institutions.Any(x => x >= TransitLicenseRules.AnchorConfidence) && fields >= TransitLicenseRules.MinimumFields
            && average >= TransitLicenseRules.ValidConfidence && score >= TransitLicenseRules.ValidScore;
        var signals = title > 0 || Match("LICENCIA") > 0 || institutions.Any(x => x > 0) && fields >= 3;
        return new(valid ? TransitLicenseStatus.VALID : signals ? TransitLicenseStatus.REVIEW_REQUIRED : TransitLicenseStatus.INVALID,
            score, fields, average);
    }
    private static double Quality(double value) => double.IsFinite(value) && value is >= 0 and <= 100 ? value : 0;
    private static string[] Tokens(string text)
    {
        var normalized = new string(text.Normalize(NormalizationForm.FormD).Where(x => CharUnicodeInfo.GetUnicodeCategory(x)
            != UnicodeCategory.NonSpacingMark).ToArray()).ToUpperInvariant();
        return Regex.Replace(normalized, "[^A-Z0-9]+", " ").Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Where(x => x is not ("DE" or "LA" or "EL")).ToArray();
    }
    private static bool ContainsPhrase(string[] tokens, string[] phrase)
    {
        for (var start = 0; start + phrase.Length <= tokens.Length; start++)
            if (phrase.Select((word, index) => Similar(tokens[start + index], word)).All(x => x)) return true;
        return false;
    }
    private static bool Similar(string actual, string expected)
    {
        if (actual == expected) return true;
        if (expected.Length < 5 || Math.Abs(actual.Length - expected.Length) > 1) return false;
        var previous = Enumerable.Range(0, expected.Length + 1).ToArray();
        for (var i = 1; i <= actual.Length; i++)
        {
            var row = new int[expected.Length + 1]; row[0] = i;
            for (var j = 1; j <= expected.Length; j++) row[j] = Math.Min(Math.Min(row[j - 1] + 1, previous[j] + 1), previous[j - 1] + (actual[i - 1] == expected[j - 1] ? 0 : 1));
            previous = row;
        }
        return previous[expected.Length] <= 1;
    }
}
