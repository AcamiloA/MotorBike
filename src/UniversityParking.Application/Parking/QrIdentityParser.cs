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
        "El código QR no contiene una identificación válida.", ErrorType.Validation);

    public Result<IdentificationNumber> Parse(string? payload)
    {
        if (payload is null || payload.Length > MaximumPayloadLength) return Invalid();
        var raw = payload.AsSpan().Trim();
        Span<byte> buffer = stackalloc byte[192];
        if (raw.IsEmpty || !Convert.TryFromBase64Chars(raw, buffer, out var count)) return Invalid();
        try
        {
            var value = StrictUtf8.GetString(buffer[..count]).Trim();
            // Institutional identifiers are text tokens, never structured payloads or claims.
            if (value.Length is 0 or > 50 || value.Any(c => !char.IsLetterOrDigit(c) && c != '-')) return Invalid();
            return Result<IdentificationNumber>.Success(new(value));
        }
        catch (DecoderFallbackException) { return Invalid(); }
        catch (DomainException) { return Invalid(); }
    }
    private static Result<IdentificationNumber> Invalid() => Result<IdentificationNumber>.Failure(InvalidIdentity);
}
