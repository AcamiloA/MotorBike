using System.Text.Json.Serialization;

namespace UniversityParking.Contracts.Users;

public enum UserMemberType { STUDENT, TEACHER, STAFF }
public enum UserAccountStatus { ACTIVE, INACTIVE }
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record CreateUserRequest(string IdentificationNumber, string FullName, [property: JsonRequired] Guid UniversityId,
    string? Career, [property: JsonRequired] UserMemberType MemberType, string CardCode, string InitialPassword, IReadOnlyCollection<string>? Roles = null);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record UpdateMyProfileRequest(string FullName, string? Career);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record UpdateUserRequest(string FullName, [property: JsonRequired] Guid UniversityId, string? Career, [property: JsonRequired] UserMemberType MemberType, string CardCode);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record AssignRoleRequest(string Role);
public sealed record UserCreatedResponse(Guid Id);
public sealed record UserProfileResponse(Guid Id, string IdentificationNumber, string FullName, Guid UniversityId, string UniversityName,
    string? Career, string MemberType, string CardCode, string Status, IReadOnlyCollection<string> Roles);
public sealed record UserListItemResponse(Guid Id, string IdentificationNumber, string FullName, Guid UniversityId, string UniversityName,
    string? Career, string MemberType, string CardCode, string Status, IReadOnlyCollection<string> Roles);

