using System.Text;
using UniversityParking.Application.Common.Results;
using UniversityParking.Domain.Common;
using UniversityParking.Domain.Users.ValueObjects;

namespace UniversityParking.Application.Parking;

public interface IQrIdentityParser
{
    Result<IdentificationNumber> Parse(string? payload);
}

public sealed class QrIdentityParser : IQrIdentityParser
{
    public const int MaximumPayloadLength = 256;
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);
    public static Error InvalidIdentity { get; } = new("INVALID_QR_IDENTITY",
        "El código no contiene una identificación válida.", ErrorType.Validation);

    public Result<IdentificationNumber> Parse(string? payload)
    {
        if (payload is null || payload.Length > MaximumPayloadLength) return Invalid();
        var raw = payload.AsSpan().Trim();
        Span<byte> buffer = stackalloc byte[192];
        if (raw.IsEmpty) return Invalid();
        if (Convert.TryFromBase64Chars(raw, buffer, out var count)) try
        {
            var value = StrictUtf8.GetString(buffer[..count]).Trim();
            // Institutional identifiers are text tokens, never structured payloads or claims.
            if (ValidToken(value)) return Result<IdentificationNumber>.Success(new(value));
            // Encoded JSON/claims are never interpreted as a plain identity or permissions.
            if (value.IndexOfAny(['{','}','[',']',':','=',',','"']) >= 0) return Invalid();
        }
        catch (DecoderFallbackException) { /* A plain identifier may also resemble Base64. */ }
        catch (DomainException) { return Invalid(); }
        var plain = raw.ToString();
        return ValidToken(plain) ? Result<IdentificationNumber>.Success(new(plain)) : Invalid();
    }
    private static bool ValidToken(string value) => value.Length is > 0 and <= 50 &&
        value.All(c => char.IsLetterOrDigit(c) || c == '-');
    private static Result<IdentificationNumber> Invalid() => Result<IdentificationNumber>.Failure(InvalidIdentity);
}
