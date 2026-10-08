using UniversityParking.Domain.Common;
using UniversityParking.Domain.Users.ValueObjects;

namespace UniversityParking.Domain.Users;

public sealed class User : Entity
{
    public IdentificationNumber IdentificationNumber { get; private set; }
    public string FullName { get; private set; }
    public string University { get; private set; }
    public string? Career { get; private set; }
    public MemberType MemberType { get; private set; }
    public CardCode CardCode { get; private set; }
    public UserStatus Status { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    public User(IdentificationNumber identificationNumber, string fullName, string university,
        string? career, MemberType memberType, CardCode cardCode, DateTimeOffset createdAt)
    {
        ArgumentNullException.ThrowIfNull(identificationNumber);
        ArgumentNullException.ThrowIfNull(cardCode);
        IdentificationNumber = identificationNumber;
        FullName = Guard.Text(fullName, 200, "nombre");
        University = Guard.Text(university, 200, "universidad");
        MemberType = Guard.Defined(memberType);
        Career = ValidateCareer(career, memberType);
        CardCode = cardCode;
        Status = UserStatus.ACTIVE;
        CreatedAt = UpdatedAt = Guard.Utc(createdAt);
    }

    public void UpdateProfile(string fullName, string? career, DateTimeOffset updatedAt) =>
        Update(fullName, University, career, MemberType, CardCode, updatedAt);

    // Cross-aggregate eligibility is checked by Application before this method.
    public void Update(string fullName, string university, string? career, MemberType memberType,
        CardCode cardCode, DateTimeOffset updatedAt)
    {
        ArgumentNullException.ThrowIfNull(cardCode);
        var name = Guard.Text(fullName, 200, "nombre");
        var institution = Guard.Text(university, 200, "universidad");
        Guard.Defined(memberType);
        var validCareer = ValidateCareer(career, memberType);
        var now = Guard.Utc(updatedAt);
        Guard.Chronology(now, UpdatedAt);
        FullName = name;
        University = institution;
        Career = validCareer;
        MemberType = memberType;
        CardCode = cardCode;
        UpdatedAt = now;
    }

    public void Activate(DateTimeOffset now) => SetStatus(UserStatus.ACTIVE, now);
    public void Deactivate(DateTimeOffset now) => SetStatus(UserStatus.INACTIVE, now);

    private void SetStatus(UserStatus status, DateTimeOffset now)
    {
        if (Status == status) return;
        var utc = Guard.Utc(now);
        Guard.Chronology(utc, UpdatedAt);
        Status = status;
        UpdatedAt = utc;
    }

    private static string? ValidateCareer(string? career, MemberType type) => type == MemberType.STUDENT
        ? Guard.Text(career, 200, "carrera")
        : Guard.OptionalText(career, 200, "carrera");
}
