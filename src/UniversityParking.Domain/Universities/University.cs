using UniversityParking.Domain.Common;

namespace UniversityParking.Domain.Universities;

public sealed class University : Entity
{
    public const int CodeMaximumLength = 20;
    public const int NameMaximumLength = 200;

    public string Code { get; private set; }
    public string Name { get; private set; }
    public bool IsActive { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    public University(Guid id, string code, string name, DateTimeOffset createdAt) : base(id)
    {
        Code = Guard.Text(Guard.Text(code, CodeMaximumLength, "código de universidad").ToUpperInvariant(),
            CodeMaximumLength, "código de universidad");
        Name = Guard.Text(name, NameMaximumLength, "nombre de universidad");
        IsActive = true;
        CreatedAt = UpdatedAt = Guard.Utc(createdAt);
    }

    public void Activate(DateTimeOffset now) => SetActive(true, now);
    public void Deactivate(DateTimeOffset now) => SetActive(false, now);

    private void SetActive(bool active, DateTimeOffset now)
    {
        if (IsActive == active) return;
        var utc = Guard.Utc(now);
        Guard.Chronology(utc, UpdatedAt);
        IsActive = active;
        UpdatedAt = utc;
    }
}
