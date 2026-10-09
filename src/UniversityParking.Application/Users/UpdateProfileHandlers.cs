using MediatR;
using UniversityParking.Application.Common.Abstractions;
using UniversityParking.Application.Common.Errors;
using UniversityParking.Application.Common.Results;
using UniversityParking.Domain.Users;
using UniversityParking.Domain.Users.ValueObjects;

namespace UniversityParking.Application.Users;

public sealed class UpdateMyProfileCommandHandler(UserOperationContext operation, IUserRepository users, IUnitOfWork unitOfWork, IUniversityRepository universities)
    : IRequestHandler<UpdateMyProfileCommand, Result>
{
    public async Task<Result> Handle(UpdateMyProfileCommand request, CancellationToken cancellationToken)
    {
        if (await operation.CheckAccessAsync(false, cancellationToken) is { } error) return Result.Failure(error);
        var user = (await users.GetByIdAsync(operation.ActorId!.Value, cancellationToken))!;
        if (user.MemberType == MemberType.STUDENT && string.IsNullOrWhiteSpace(request.Career))
            return Result.Failure(CommonErrors.Validation(new Dictionary<string, IReadOnlyList<string>>
                { [nameof(request.Career)] = ["La carrera es obligatoria para estudiantes."] }));
        var currentUniversity = await universities.GetByIdAsync(user.UniversityId, cancellationToken);
        if (currentUniversity is null) return Result.Failure(UniversityErrors.NotFound);
        var before = UserOperationContext.Snapshot(user, currentUniversity.Name);
        if(request.Email is not null || request.PhoneNumber is not null)
        {
            var email=ContactInformation.Email(request.Email!);
            var existing=await users.GetByEmailAsync(email,cancellationToken);
            if(existing is not null && existing.Id!=user.Id) return Result.Failure(new("EMAIL_ALREADY_EXISTS","El correo ya está registrado.",ErrorType.Conflict));
            user.SetContact(email,request.PhoneNumber!);
        }
        user.UpdateProfile(request.FullName, request.Career, operation.UtcNow);
        await operation.AuditAsync("USER_UPDATED", user.Id, before, UserOperationContext.Snapshot(user, currentUniversity.Name), cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
public sealed class UpdateUserCommandHandler(UserOperationContext operation, IUserRepository users,
    IVehicleRepository vehicles, IUnitOfWork unitOfWork, IUniversityRepository universities,IRoleRepository? roles=null,IUserCredentialRepository? credentials=null) : IRequestHandler<UpdateUserCommand, Result>
{
    public async Task<Result> Handle(UpdateUserCommand request, CancellationToken cancellationToken)
    {
        if (await operation.CheckAccessAsync(true, cancellationToken) is { } error) return Result.Failure(error);
        var user = await users.GetByIdAsync(request.UserId, cancellationToken);
        if (user is null) return Result.Failure(UserErrors.NotFound);
        var card = user.CardCode;
        var email=ContactInformation.Email(request.Email!);
        var existing=await users.GetByEmailAsync(email,cancellationToken);
        if(existing is not null && existing.Id!=user.Id) return Result.Failure(new("EMAIL_ALREADY_EXISTS","El correo ya está registrado.",ErrorType.Conflict));
        var type=request.UserType??user.UserType;
        if(type!=user.UserType && user.Status is UserStatus.PENDING or UserStatus.REJECTED)
            return Result.Failure(new("INVALID_USER_STATUS_TRANSITION","Primero resuelve la solicitud de registro antes de cambiar el tipo institucional.",ErrorType.Conflict));
        var member=type==InstitutionalUserType.STUDENT?MemberType.STUDENT:type==InstitutionalUserType.TEACHER?MemberType.TEACHER:MemberType.STAFF;
        var cardOwner = await users.GetByCardCodeAsync(card, cancellationToken);
        if (cardOwner is not null && cardOwner.Id != user.Id) return Result.Failure(UserErrors.CardCodeAlreadyExists);
        if (type == InstitutionalUserType.STUDENT && user.UserType != InstitutionalUserType.STUDENT &&
            await vehicles.HasActiveCarOwnedByUserAsync(user.Id, cancellationToken)) return Result.Failure(UserErrors.InvalidMemberTypeChange);
        var currentUniversity = await universities.GetByIdAsync(user.UniversityId, cancellationToken);
        if (currentUniversity is null) return Result.Failure(UniversityErrors.NotFound);
        var before = UserOperationContext.Snapshot(user, currentUniversity.Name);
        var universityId=request.UniversityId==Guid.Empty && type is InstitutionalUserType.ADMINISTRATIVE or InstitutionalUserType.GUARD?user.UniversityId:request.UniversityId;
        var university = await universities.GetByIdAsync(universityId, cancellationToken);
        if (university is null) return Result.Failure(UniversityErrors.NotFound);
        if (!university.IsActive && university.Id != user.UniversityId) return Result.Failure(UniversityErrors.Inactive);
        user.Update(request.FullName, university.Id, request.Career, member, card, operation.UtcNow);
        user.SetContact(email,request.PhoneNumber!);
        if(type!=user.UserType)
        {
            if(roles is null) throw new InvalidOperationException();
            var derivedCode=InstitutionalUsers.Role(type);var currentCodes=await roles.GetCodesByUserIdAsync(user.Id,cancellationToken);
            foreach(var code in currentCodes.Where(code=>code!=derivedCode))
            {
                var assigned=await roles.GetByCodeAsync(code,cancellationToken);
                if(assigned is not null) await roles.RemoveAsync(user.Id,assigned.Id,cancellationToken);
            }
            if(!currentCodes.Contains(derivedCode))
            {var derived=await roles.GetByCodeAsync(derivedCode,cancellationToken)??throw new InvalidOperationException();await roles.AssignAsync(new UserRole(user.Id,derived.Id),cancellationToken);}
            user.SetInstitutionalType(type);
            if(credentials is not null)(await credentials.GetByUserIdAsync(user.Id,cancellationToken))?.RotateSecurityStamp();
        }
        await operation.AuditAsync("USER_UPDATED", user.Id, before, UserOperationContext.Snapshot(user, university.Name), cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
