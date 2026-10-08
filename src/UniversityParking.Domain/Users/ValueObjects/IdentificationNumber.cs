using UniversityParking.Domain.Common;

namespace UniversityParking.Domain.Users.ValueObjects;

public sealed record IdentificationNumber
{
    public string Value { get; }

    public IdentificationNumber(string value) => Value = Guard.Text(value, 50, "identificación");

    public override string ToString() => Value;
}
