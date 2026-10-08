using MediatR;
using Microsoft.Extensions.Logging;
using UniversityParking.Application.Common.Abstractions;
using UniversityParking.Application.Common.Authorization;
using UniversityParking.Application.Common.Errors;
using UniversityParking.Application.Common.Results;
using UniversityParking.Application.Files;
using UniversityParking.Domain.Incidents;
using UniversityParking.Domain.Users;

namespace UniversityParking.Application.Incidents;

public sealed class CreateIncidentCommandHandler(AdministrationOperationContext operation, ICurrentUser actor,
    IUserRepository users, IVehicleRepository vehicles, IParkingMovementRepository movements, IParkingLotRepository lots,
    IIncidentRepository incidents, IUnitOfWork unitOfWork, FileUploadValidator validator, IFileStorage storage,
    ILogger<UploadedFileBatch> logger) : IRequestHandler<CreateIncidentCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(CreateIncidentCommand request, CancellationToken cancellationToken)
    {
        if (await operation.CheckAccessAsync([RoleCodes.Guard, RoleCodes.Admin], cancellationToken) is { } permission) return Result<Guid>.Failure(permission);
        var files = new List<ValidatedUpload>();
        foreach (var source in request.Attachments ?? [])
        {
            var validation = await validator.ValidateAsync(source, false, cancellationToken);
            if (validation.IsFailure) return Result<Guid>.Failure(validation.Error!);
            files.Add(validation.Value);
        }
        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        await operation.LockActorAsync(cancellationToken);
        if (await operation.CheckAccessAsync([RoleCodes.Guard, RoleCodes.Admin], cancellationToken) is { } lockedPermission) return Result<Guid>.Failure(lockedPermission);
        if (await lots.GetByIdAsync(request.ParkingLotId, cancellationToken) is null) return Result<Guid>.Failure(ParkingErrors.LotNotFound);
        if (request.UserId is { } userId && await users.GetByIdAsync(userId, cancellationToken) is null) return Result<Guid>.Failure(UserErrors.NotFound);
        if (request.VehicleId is { } vehicleId && await vehicles.GetByIdAsync(vehicleId, cancellationToken) is null) return Result<Guid>.Failure(VehicleErrors.NotFound);
        if (request.ParkingMovementId is { } movementId)
        {
            var movement = await movements.GetByIdAsync(movementId, cancellationToken);
            if (movement is null) return Result<Guid>.Failure(new("PARKING_MOVEMENT_NOT_FOUND", "El movimiento no existe.", ErrorType.NotFound));
            if (movement.ParkingLotId != request.ParkingLotId || request.UserId.HasValue && movement.UserId != request.UserId.Value ||
                request.VehicleId.HasValue && movement.VehicleId != request.VehicleId.Value)
                return Result<Guid>.Failure(CommonErrors.Validation(new Dictionary<string, IReadOnlyList<string>>
                { ["parkingMovementId"] = ["Las referencias no corresponden al movimiento."] }));
        }
        var now = operation.UtcNow;
        var incident = new Incident(request.ParkingLotId, actor.UserId!.Value, request.Type, request.Description,
            (request.OccurredAt ?? now).ToUniversalTime(), now, request.UserId, request.VehicleId, request.ParkingMovementId);
        await using var uploads = new UploadedFileBatch(storage, logger);
        await incidents.AddAsync(incident, cancellationToken);
        foreach (var file in files)
        {
            var key = await uploads.UploadIncidentAsync(incident.Id, file, cancellationToken);
            await incidents.AddAttachmentAsync(new(incident.Id, key, file.FileName, file.ContentType, file.Bytes.LongLength, now), cancellationToken);
        }
        await operation.AuditAsync("INCIDENT_CREATED", "Incident", incident.Id, null,
            new { incident.Status, incident.Type, incident.UserId, incident.VehicleId, incident.ParkingMovementId, incident.ParkingLotId }, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        uploads.MarkCommitStarted();
        await transaction.CommitAsync(cancellationToken);
        uploads.MarkCommitted();
        return Result<Guid>.Success(incident.Id);
    }
}
public sealed class ResolveIncidentCommandHandler(AdministrationOperationContext operation, ICurrentUser actor,
    IIncidentRepository incidents, IUnitOfWork unitOfWork) : IRequestHandler<ResolveIncidentCommand, Result>
{
    public async Task<Result> Handle(ResolveIncidentCommand request, CancellationToken cancellationToken)
    {
        if (await operation.CheckAccessAsync([RoleCodes.Admin], cancellationToken) is { } permission) return Result.Failure(permission);
        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        await operation.LockActorAsync(cancellationToken);
        if (await operation.CheckAccessAsync([RoleCodes.Admin], cancellationToken) is { } lockedPermission) return Result.Failure(lockedPermission);
        var incident = await incidents.GetByIdForUpdateAsync(request.IncidentId, cancellationToken);
        if (incident is null) return Result.Failure(IncidentErrors.NotFound);
        if (incident.Status == IncidentStatus.RESOLVED) return Result.Failure(IncidentErrors.AlreadyResolved);
        if (incident.Status != IncidentStatus.OPEN) return Result.Failure(IncidentErrors.NotOpen);
        var before = new { incident.Status };
        incident.Resolve(request.Resolution, actor.UserId!.Value, operation.UtcNow);
        await operation.AuditAsync("INCIDENT_RESOLVED", "Incident", incident.Id, before,
            new { incident.Status, incident.ResolvedBy, incident.ResolvedAt }, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return Result.Success();
    }
}
public sealed class CancelIncidentCommandHandler(AdministrationOperationContext operation, IIncidentRepository incidents,
    IUnitOfWork unitOfWork) : IRequestHandler<CancelIncidentCommand, Result>
{
    public async Task<Result> Handle(CancelIncidentCommand request, CancellationToken cancellationToken)
    {
        if (await operation.CheckAccessAsync([RoleCodes.Admin], cancellationToken) is { } permission) return Result.Failure(permission);
        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        await operation.LockActorAsync(cancellationToken);
        if (await operation.CheckAccessAsync([RoleCodes.Admin], cancellationToken) is { } lockedPermission) return Result.Failure(lockedPermission);
        var incident = await incidents.GetByIdForUpdateAsync(request.IncidentId, cancellationToken);
        if (incident is null) return Result.Failure(IncidentErrors.NotFound);
        if (incident.Status == IncidentStatus.CANCELLED) return Result.Success();
        if (incident.Status != IncidentStatus.OPEN) return Result.Failure(IncidentErrors.NotOpen);
        var before = new { incident.Status };
        incident.Cancel(operation.UtcNow, request.Reason);
        await operation.AuditAsync("INCIDENT_CANCELLED", "Incident", incident.Id, before, new { incident.Status }, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return Result.Success();
    }
}
