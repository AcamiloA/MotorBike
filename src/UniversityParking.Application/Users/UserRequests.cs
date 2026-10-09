using UniversityParking.Domain.Users;
using UniversityParking.Application.Common.Messaging;
using UniversityParking.Application.Common.Pagination;

namespace UniversityParking.Application.Users;

public interface IUserWriteCommand;
public sealed record ApproveStudentRegistrationCommand(Guid UserId) : ICommand, IUserWriteCommand;
public sealed record RejectStudentRegistrationCommand(Guid UserId) : ICommand, IUserWriteCommand;
public sealed record CreateUserCommand(string IdentificationNumber, string FullName, Guid UniversityId,
    string? Career, MemberType MemberType, string CardCode, string InitialPassword,
    IReadOnlyCollection<string>? Roles = null, InstitutionalUserType UserType = InstitutionalUserType.STUDENT,
    string? Email = null, string? PhoneNumber = null,string? IdentificationType=null) : ICommand<Guid>, IUserWriteCommand;
public sealed record UpdateMyProfileCommand(string FullName, string? Career,string? Email=null,string? PhoneNumber=null) : ICommand, IUserWriteCommand;
public sealed record UpdateUserCommand(Guid UserId, string FullName, Guid UniversityId, string? Career,
    MemberType MemberType, string? CardCode,InstitutionalUserType? UserType=null,string? Email=null,string? PhoneNumber=null) : ICommand, IUserWriteCommand;
public sealed record ActivateUserCommand(Guid UserId) : ICommand, IUserWriteCommand;
public sealed record DeactivateUserCommand(Guid UserId) : ICommand, IUserWriteCommand;
public sealed record AssignRoleCommand(Guid UserId, string RoleCode) : ICommand, IUserWriteCommand;
public sealed record RemoveRoleCommand(Guid UserId, string RoleCode) : ICommand, IUserWriteCommand;
public sealed record GetMyProfileQuery : IQuery<UserProfile>;
public sealed record GetUserByIdQuery(Guid UserId) : IQuery<UserProfile>;
public sealed record GetUsersQuery(string? Search = null, MemberType? MemberType = null,
    UserStatus? Status = null, string? Role = null, int Page = 1, int PageSize = 20,InstitutionalUserType? UserType=null) : IQuery<PagedResult<UserProfile>>;
public sealed record UserProfile(Guid Id, string IdentificationNumber, string FullName, Guid UniversityId, string UniversityName,
    string? Career, MemberType MemberType, string CardCode, UserStatus Status, IReadOnlyCollection<string> Roles,
    InstitutionalUserType UserType=InstitutionalUserType.STUDENT,string? Email=null,string? PhoneNumber=null,bool MustChangePassword=false)
{
    public static UserProfile From(User user, IReadOnlyCollection<string> roles, string universityName) => new(user.Id,
        user.IdentificationNumber.Value, user.FullName, user.UniversityId, universityName, user.Career, user.MemberType,
        user.CardCode.Value, user.Status, roles,user.UserType,user.Email,user.PhoneNumber,user.MustChangePassword);
}

