using MediatR;
using UniversityParking.Application.Common.Abstractions;
using UniversityParking.Application.Common.Authorization;
using UniversityParking.Application.Common.Errors;
using UniversityParking.Application.Common.Results;
using UniversityParking.Domain.AcademicPeriods;
using UniversityParking.Domain.Users;

namespace UniversityParking.Application.AcademicPeriods;

public sealed class CreateAcademicPeriodCommandHandler(AdministrationOperationContext operation, IAcademicPeriodRepository periods, IUnitOfWork unitOfWork)
    : IRequestHandler<CreateAcademicPeriodCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(CreateAcademicPeriodCommand request, CancellationToken cancellationToken)
    {
        if (await operation.CheckAccessAsync([RoleCodes.Admin], cancellationToken) is { } error) return Result<Guid>.Failure(error);
        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        await operation.LockActorAsync(cancellationToken);
        var name = request.Name.Trim();
        if (await periods.ExistsByNameAsync(name, cancellationToken)) return Result<Guid>.Failure(AcademicPeriodErrors.NameAlreadyExists);
        var period = new AcademicPeriod(name, request.StartsOn, request.EndsOn, operation.UtcNow);
        await periods.AddAsync(period, cancellationToken);
        await operation.AuditAsync("ACADEMIC_PERIOD_CREATED", "AcademicPeriod", period.Id, null,
            new { period.Name, period.StartsOn, period.EndsOn, Status = "PLANNED" }, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return Result<Guid>.Success(period.Id);
    }
}
public sealed class ActivateAcademicPeriodCommandHandler(AdministrationOperationContext operation, IAcademicPeriodRepository periods, IUnitOfWork unitOfWork)
    : IRequestHandler<ActivateAcademicPeriodCommand, Result>
{
    public async Task<Result> Handle(ActivateAcademicPeriodCommand request, CancellationToken cancellationToken)
    {
        if (await operation.CheckAccessAsync([RoleCodes.Admin], cancellationToken) is { } error) return Result.Failure(error);
        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        await operation.LockActorAsync(cancellationToken);
        var period = await periods.GetByIdForUpdateAsync(request.AcademicPeriodId, cancellationToken);
        if (period is null) return Result.Failure(AcademicPeriodErrors.NotFound);
        if (period.Status == AcademicPeriodStatus.ACTIVE) return Result.Success();
        if (period.Status == AcademicPeriodStatus.CLOSED) return Result.Failure(AcademicPeriodErrors.InvalidState);
        if (await periods.ExistsActiveAsync(cancellationToken)) return Result.Failure(AcademicPeriodErrors.AnotherActivePeriodExists);
        period.Activate();
        await operation.AuditAsync("ACADEMIC_PERIOD_ACTIVATED", "AcademicPeriod", period.Id, new { Status = "PLANNED" }, new { Status = "ACTIVE" }, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return Result.Success();
    }
}
public sealed class CloseAcademicPeriodCommandHandler(AdministrationOperationContext operation, IAcademicPeriodRepository periods, IUnitOfWork unitOfWork)
    : IRequestHandler<CloseAcademicPeriodCommand, Result>
{
    public async Task<Result> Handle(CloseAcademicPeriodCommand request, CancellationToken cancellationToken)
    {
        if (await operation.CheckAccessAsync([RoleCodes.Admin], cancellationToken) is { } error) return Result.Failure(error);
        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        await operation.LockActorAsync(cancellationToken);
        var period = await periods.GetByIdForUpdateAsync(request.AcademicPeriodId, cancellationToken);
        if (period is null) return Result.Failure(AcademicPeriodErrors.NotFound);
        if (period.Status == AcademicPeriodStatus.CLOSED) return Result.Success();
        if (period.Status != AcademicPeriodStatus.ACTIVE) return Result.Failure(AcademicPeriodErrors.InvalidState);
        period.Close();
        await operation.AuditAsync("ACADEMIC_PERIOD_CLOSED", "AcademicPeriod", period.Id, new { Status = "ACTIVE" }, new { Status = "CLOSED" }, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return Result.Success();
    }
}
public sealed class GetCurrentAcademicPeriodQueryHandler(AdministrationOperationContext operation, IAcademicPeriodRepository periods)
    : IRequestHandler<GetCurrentAcademicPeriodQuery, Result<AcademicPeriodView>>
{
    public async Task<Result<AcademicPeriodView>> Handle(GetCurrentAcademicPeriodQuery request, CancellationToken cancellationToken)
    {
        if (await operation.CheckAccessAsync([], cancellationToken) is { } error) return Result<AcademicPeriodView>.Failure(error);
        var period = await periods.GetActiveAsync(cancellationToken);
        return period is null ? Result<AcademicPeriodView>.Failure(AcademicPeriodErrors.CurrentNotFound) : Result<AcademicPeriodView>.Success(AcademicPeriodView.From(period));
    }
}
public sealed class GetAcademicPeriodsQueryHandler(AdministrationOperationContext operation, IAcademicPeriodRepository periods)
    : IRequestHandler<GetAcademicPeriodsQuery, Result<IReadOnlyList<AcademicPeriodView>>>
{
    public async Task<Result<IReadOnlyList<AcademicPeriodView>>> Handle(GetAcademicPeriodsQuery request, CancellationToken cancellationToken)
    {
        if (await operation.CheckAccessAsync([RoleCodes.Admin], cancellationToken) is { } error) return Result<IReadOnlyList<AcademicPeriodView>>.Failure(error);
        return Result<IReadOnlyList<AcademicPeriodView>>.Success((await periods.GetAllAsync(cancellationToken)).Select(AcademicPeriodView.From).ToArray());
    }
}

