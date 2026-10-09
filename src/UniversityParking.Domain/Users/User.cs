using UniversityParking.Domain.Common;
using UniversityParking.Domain.Users.ValueObjects;

namespace UniversityParking.Domain.Users;

public sealed class User : Entity
{
    public IdentificationNumber IdentificationNumber { get; private set; }
    public string FullName { get; private set; }
    public Guid UniversityId { get; private set; }
    public string? Career { get; private set; }
    public MemberType MemberType { get; private set; }
    public CardCode CardCode { get; private set; }
    public UserStatus Status { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    public User(IdentificationNumber identificationNumber, string fullName, Guid universityId,
        string? career, MemberType memberType, CardCode cardCode, DateTimeOffset createdAt)
    {
        ArgumentNullException.ThrowIfNull(identificationNumber);
        ArgumentNullException.ThrowIfNull(cardCode);
        IdentificationNumber = identificationNumber;
        FullName = Guard.Text(fullName, 200, "nombre");
        UniversityId = Guard.Id(universityId, "universidad");
        MemberType = Guard.Defined(memberType);
        Career = ValidateCareer(career, memberType);
        CardCode = cardCode;
        Status = UserStatus.ACTIVE;
        CreatedAt = UpdatedAt = Guard.Utc(createdAt);
    }

    public static User CreateStudentRegistration(IdentificationNumber identificationNumber, string fullName,
        Guid universityId, string career, CardCode cardCode, bool autoApprove, DateTimeOffset createdAt)
    {
        var user = new User(identificationNumber, fullName, universityId, career, MemberType.STUDENT, cardCode, createdAt);
        if (!autoApprove) user.Status = UserStatus.PENDING;
        return user;
    }

    public void ApproveRegistration(DateTimeOffset now) => ReviewRegistration(UserStatus.ACTIVE, now);
    public void RejectRegistration(DateTimeOffset now) => ReviewRegistration(UserStatus.REJECTED, now);

    private void ReviewRegistration(UserStatus next, DateTimeOffset now)
    {
        if (Status != UserStatus.PENDING || MemberType != MemberType.STUDENT) InvalidTransition();
        SetStatus(next, now);
    }

    private static void InvalidTransition() =>
        throw new DomainException("INVALID_USER_STATUS_TRANSITION", "La transición de estado del usuario no está permitida.");
    public void UpdateProfile(string fullName, string? career, DateTimeOffset updatedAt) =>
        Update(fullName, UniversityId, career, MemberType, CardCode, updatedAt);

    // Cross-aggregate eligibility is checked by Application before this method.
    public void Update(string fullName, Guid universityId, string? career, MemberType memberType,
        CardCode cardCode, DateTimeOffset updatedAt)
    {
        ArgumentNullException.ThrowIfNull(cardCode);
        var name = Guard.Text(fullName, 200, "nombre");
        var institution = Guard.Id(universityId, "universidad");
        Guard.Defined(memberType);
        var validCareer = ValidateCareer(career, memberType);
        var now = Guard.Utc(updatedAt);
        Guard.Chronology(now, UpdatedAt);
        FullName = name;
        UniversityId = institution;
        Career = validCareer;
        MemberType = memberType;
        CardCode = cardCode;
        UpdatedAt = now;
    }

    public void Activate(DateTimeOffset now) { RequireOperationalStatus(); SetStatus(UserStatus.ACTIVE, now); }
    public void Deactivate(DateTimeOffset now) { RequireOperationalStatus(); SetStatus(UserStatus.INACTIVE, now); }

    private void RequireOperationalStatus() { if (Status is not (UserStatus.ACTIVE or UserStatus.INACTIVE)) InvalidTransition(); }

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
