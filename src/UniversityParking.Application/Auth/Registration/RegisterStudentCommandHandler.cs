using System.Text.Json;
using MediatR;
using Microsoft.Extensions.Options;
using UniversityParking.Application.Common.Abstractions;
using UniversityParking.Application.Common.Errors;
using UniversityParking.Application.Common.Results;
using UniversityParking.Application.Users;
using UniversityParking.Domain.Auditing;
using UniversityParking.Domain.Users;
using UniversityParking.Domain.Users.ValueObjects;

namespace UniversityParking.Application.Auth.Registration;

public sealed class RegisterStudentCommandHandler(IUserRepository users, IUniversityRepository universities,
    IRoleRepository roles, AccountProvisioner provisioner, IAuditLogRepository audits, IUnitOfWork unitOfWork,
    IClock clock, IRequestContext requestContext, IOptions<StudentRegistrationOptions> options)
    : IRequestHandler<RegisterStudentCommand, Result<RegisterStudentResult>>
{
    public async Task<Result<RegisterStudentResult>> Handle(RegisterStudentCommand request, CancellationToken token)
    {
        var identification = new IdentificationNumber(request.IdentificationNumber);
        var card = new CardCode(Guid.NewGuid().ToString("N"));
        var email = ContactInformation.Email(request.Email!);
        if (await users.ExistsByIdentificationNumberAsync(identification, token)) return Result<RegisterStudentResult>.Failure(UserErrors.AlreadyExists);
        if(await users.GetByEmailAsync(email, token) is not null) return Result<RegisterStudentResult>.Failure(new("EMAIL_ALREADY_EXISTS","El correo ya está registrado.",ErrorType.Conflict));
        if (await users.ExistsByCardCodeAsync(card, token)) return Result<RegisterStudentResult>.Failure(UserErrors.CardCodeAlreadyExists);
        var university = await universities.GetByIdAsync(request.UniversityId, token);
        if (university is null) return Result<RegisterStudentResult>.Failure(UniversityErrors.NotFound);
        if (!university.IsActive) return Result<RegisterStudentResult>.Failure(UniversityErrors.Inactive);
        var role = await roles.GetByCodeAsync(RoleCodes.User, token);
        if (role is null) return Result<RegisterStudentResult>.Failure(new("ROLE_NOT_FOUND", "El rol solicitado no existe.", ErrorType.NotFound));
        var now = clock.UtcNow;
        var user = User.CreateStudentRegistration(identification, request.FullName, university.Id, request.Career,
            card, options.Value.AutoApprove, now);
        user.SetContact(email, request.PhoneNumber!);
        await provisioner.AddAsync(user, request.Password, [role], token);
        var values = JsonSerializer.Serialize(new { Profile = UserOperationContext.Snapshot(user, university.Name), Roles = new[] { RoleCodes.User } });
        await audits.AddAsync(new AuditLog(null, "STUDENT_REGISTERED", "User", user.Id, now,
            newValues: values, ipAddress: requestContext.IpAddress, traceId: requestContext.TraceId), token);
        await unitOfWork.SaveChangesAsync(token);
        return Result<RegisterStudentResult>.Success(new(user.Id, user.Status));
    }
}
