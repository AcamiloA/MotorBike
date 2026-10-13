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
        if (payload is null) return Invalid();
        var raw = payload.AsSpan().Trim();
        if (payload.Length > MaximumPayloadLength || raw.IsEmpty) return Invalid();
        var plain = raw.ToString();
        if (ValidToken(plain)) return Result<IdentificationNumber>.Success(new(plain));
        Span<byte> buffer = stackalloc byte[192];
        if (!Convert.TryFromBase64Chars(raw, buffer, out var count) ||
            !raw.SequenceEqual(Convert.ToBase64String(buffer[..count]).AsSpan())) return Invalid();
        try
        {
            var value = StrictUtf8.GetString(buffer[..count]).Trim();
            // Institutional identifiers are text tokens, never structured payloads or claims.
            if (ValidToken(value)) return Result<IdentificationNumber>.Success(new(value));
            return Invalid();
        }
        catch (DecoderFallbackException) { return Invalid(); }
        catch (DomainException) { return Invalid(); }
    }
    private static bool ValidToken(string value) => value.Length is > 0 and <= 50 &&
        value.All(c => char.IsLetterOrDigit(c) || c == '-');
    private static Result<IdentificationNumber> Invalid() => Result<IdentificationNumber>.Failure(InvalidIdentity);
}
