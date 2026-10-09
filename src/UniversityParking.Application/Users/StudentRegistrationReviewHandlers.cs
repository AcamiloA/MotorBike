using MediatR;
using UniversityParking.Application.Common.Abstractions;
using UniversityParking.Application.Common.Errors;
using UniversityParking.Application.Common.Results;
using UniversityParking.Domain.Users;

namespace UniversityParking.Application.Users;

public sealed class StudentRegistrationReview(UserOperationContext operation, IUserRepository users,
    IUniversityRepository universities, IUnitOfWork work)
{
    public async Task<Result> ReviewAsync(Guid userId, bool approve, CancellationToken token)
    {
        if (await operation.CheckAccessAsync(true, token) is { } error) return Result.Failure(error);
        var user = await users.GetByIdAsync(userId, token);
        if (user is null) return Result.Failure(UserErrors.NotFound);
        if (user.Status != UserStatus.PENDING || user.MemberType != MemberType.STUDENT) return Result.Failure(UserErrors.InvalidStatusTransition);
        var university = await universities.GetByIdAsync(user.UniversityId, token);
        if (university is null) return Result.Failure(UniversityErrors.NotFound);
        var before = new { Status = user.Status.ToString(), user.UniversityId, UniversityName = university.Name };
        if (approve) user.ApproveRegistration(operation.UtcNow); else user.RejectRegistration(operation.UtcNow);
        await operation.AuditAsync(approve ? "STUDENT_REGISTRATION_APPROVED" : "STUDENT_REGISTRATION_REJECTED", user.Id,
            before, new { Status = user.Status.ToString(), user.UniversityId, UniversityName = university.Name }, token);
        await work.SaveChangesAsync(token);
        return Result.Success();
    }
}
public sealed class ApproveStudentRegistrationCommandHandler(StudentRegistrationReview review)
    : IRequestHandler<ApproveStudentRegistrationCommand, Result>
{
    public Task<Result> Handle(ApproveStudentRegistrationCommand request, CancellationToken token) => review.ReviewAsync(request.UserId, true, token);
}
public sealed class RejectStudentRegistrationCommandHandler(StudentRegistrationReview review)
    : IRequestHandler<RejectStudentRegistrationCommand, Result>
{
    public Task<Result> Handle(RejectStudentRegistrationCommand request, CancellationToken token) => review.ReviewAsync(request.UserId, false, token);
}
