using System.Net.Mail;
using System.Text.RegularExpressions;
using UniversityParking.Domain.Common;

namespace UniversityParking.Domain.Users;

public static class ContactInformation
{
    public static string Email(string value)
    {
        var normalized = Guard.Text(value, 254, "correo").ToLowerInvariant();
        if (!MailAddress.TryCreate(normalized, out var address) || address.Address != normalized ||
            !normalized.Contains('@') || !address.Host.Contains('.') || normalized.Any(char.IsWhiteSpace))
            throw new DomainException("VALIDATION_ERROR", "El correo no es válido.");
        return normalized;
    }
    public static string Phone(string value)
    {
        var normalized = Regex.Replace(Guard.Text(value, 40, "teléfono"), @"[\s().-]", "");
        if (!Regex.IsMatch(normalized, @"^\+?[0-9]{7,15}$"))
            throw new DomainException("VALIDATION_ERROR", "El teléfono no es válido.");
        return normalized;
    }
}
