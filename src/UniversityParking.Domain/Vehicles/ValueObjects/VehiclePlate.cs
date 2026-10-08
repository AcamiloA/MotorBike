using UniversityParking.Domain.Common;

namespace UniversityParking.Domain.Vehicles.ValueObjects;

public sealed record VehiclePlate
{
    public string Value { get; }

    public VehiclePlate(string value)
    {
        var normalized = string.Concat((value ?? "").Where(c => !char.IsWhiteSpace(c) && c != '-')).ToUpperInvariant();
        Value = Guard.Text(normalized, 30, "placa");
    }

    public override string ToString() => Value;
}
