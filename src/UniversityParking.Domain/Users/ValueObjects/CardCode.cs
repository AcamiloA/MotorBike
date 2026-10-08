using UniversityParking.Domain.Common;

namespace UniversityParking.Domain.Users.ValueObjects;

public sealed record CardCode
{
    public string Value { get; }

    public CardCode(string value) => Value = Guard.Text(value, 150, "código de carné");

    public override string ToString() => Value;
}
