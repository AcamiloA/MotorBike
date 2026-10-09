using System.Text.Json.Serialization;

namespace UniversityParking.Contracts.Users;

public enum UserMemberType { STUDENT, TEACHER, STAFF }
public enum UserInstitutionalType { STUDENT, TEACHER, ADMINISTRATIVE, GUARD }
public enum UserAccountStatus { ACTIVE = 0, INACTIVE = 1, PENDING = 2, REJECTED = 3 }
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record CreateUserRequest(string IdentificationNumber, string FullName, Guid UniversityId,
    string? Career, UserMemberType MemberType, string? CardCode, string InitialPassword, IReadOnlyCollection<string>? Roles = null,
    [property: JsonRequired] UserInstitutionalType UserType = UserInstitutionalType.STUDENT,string? Email = null,string? PhoneNumber = null,string? IdentificationType=null);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record UpdateMyProfileRequest(string FullName, string? Career,string? Email=null,string? PhoneNumber=null);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record UpdateUserRequest(string FullName, [property: JsonRequired] Guid UniversityId, string? Career, UserMemberType MemberType, string? CardCode,
    UserInstitutionalType? UserType=null,string? Email=null,string? PhoneNumber=null);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record AssignRoleRequest(string Role);
public sealed record UserCreatedResponse(Guid Id);
public sealed record UserProfileResponse(Guid Id, string IdentificationNumber, string FullName, Guid UniversityId, string UniversityName,
    string? Career, string MemberType, string CardCode, string Status, IReadOnlyCollection<string> Roles,
    string UserType="STUDENT",string? Email=null,string? PhoneNumber=null,bool MustChangePassword=false);
public sealed record UserListItemResponse(Guid Id, string IdentificationNumber, string FullName, Guid UniversityId, string UniversityName,
    string? Career, string MemberType, string CardCode, string Status, IReadOnlyCollection<string> Roles,
    string UserType="STUDENT",string? Email=null,string? PhoneNumber=null,bool MustChangePassword=false);

