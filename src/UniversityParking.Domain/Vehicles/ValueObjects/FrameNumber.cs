using UniversityParking.Domain.Common;

namespace UniversityParking.Domain.Vehicles.ValueObjects;

public sealed record FrameNumber
{
    public string Value { get; }

    public FrameNumber(string value)
    {
        var normalized = string.Concat((value ?? "").Where(c => !char.IsWhiteSpace(c))).ToUpperInvariant();
        Value = Guard.Text(normalized, 100, "número de marco");
    }

    public override string ToString() => Value;
}
