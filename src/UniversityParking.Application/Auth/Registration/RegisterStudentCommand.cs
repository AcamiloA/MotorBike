using UniversityParking.Application.Common.Messaging;
using UniversityParking.Application.Users;
using UniversityParking.Domain.Users;

namespace UniversityParking.Application.Auth.Registration;

public sealed record RegisterStudentCommand(string IdentificationNumber, string FullName, Guid UniversityId,
    string Career, string? CardCode, string Password, string? Email = null, string? PhoneNumber = null) : ICommand<RegisterStudentResult>, IUserWriteCommand;

public sealed record RegisterStudentResult(Guid UserId, UserStatus Status);
