using MediatR;
using Microsoft.Extensions.Logging;
using UniversityParking.Application.Common.Abstractions;
using UniversityParking.Application.Common.Errors;
using UniversityParking.Application.Common.Results;
using UniversityParking.Application.Files;
using UniversityParking.Domain.Vehicles;
using UniversityParking.Domain.Vehicles.ValueObjects;

namespace UniversityParking.Application.Vehicles;

public sealed class RegisterVehicleCommandHandler(VehicleOperationContext operation, IVehicleRepository vehicles,
    IVehicleOwnershipRepository ownerships, IVehicleRegistrationRepository registrations, IVehicleEvidenceRepository evidence,
    IAcademicPeriodRepository periods, IUnitOfWork unitOfWork, FileUploadValidator fileValidator, IFileStorage storage,
    ILogger<UploadedFileBatch> logger, UniversityParking.Application.Documents.ITransitLicenseValidationService licenseValidation) : IRequestHandler<RegisterVehicleCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(RegisterVehicleCommand request, CancellationToken cancellationToken)
    {
        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        var actor = await operation.ActorAsync(cancellationToken, locked: true);
        if (actor.IsFailure) return Result<Guid>.Failure(actor.Error!);
        if(!await operation.HasRoleAsync(UniversityParking.Domain.Users.RoleCodes.User,cancellationToken))return Result<Guid>.Failure(CommonErrors.Forbidden);
        if (VehicleOperationContext.Eligibility(actor.Value, request.Type) is { } eligibility) return Result<Guid>.Failure(eligibility);
        var plate = request.Type is VehicleType.BICYCLE or VehicleType.SCOOTER ? null : new VehiclePlate(request.Plate!);
        var frame = request.Type is VehicleType.BICYCLE or VehicleType.SCOOTER ? new FrameNumber(request.FrameNumber!) : null;
        if (plate is not null && await vehicles.ExistsByPlateAsync(plate, cancellationToken) ||
            frame is not null && await vehicles.ExistsByFrameNumberAsync(frame, cancellationToken)) return Result<Guid>.Failure(VehicleErrors.IdentifierAlreadyExists);
        var period = await periods.GetActiveForShareAsync(cancellationToken);
        if (period is null) return Result<Guid>.Failure(AcademicPeriodErrors.NotActive);
        if (request.VerificationImage is null)
            return Result<Guid>.Failure(CommonErrors.Validation(new Dictionary<string, IReadOnlyList<string>> { ["VerificationImage"] = ["Selecciona la evidencia de verificación."] }));
        var validation = await fileValidator.ValidateAsync(request.VerificationImage, true, cancellationToken);
        if (validation.IsFailure) return Result<Guid>.Failure(validation.Error!);
        var file = validation.Value;
        if (request.Type is VehicleType.CAR or VehicleType.MOTORCYCLE)
        {
            var format = await licenseValidation.ValidateAsync(file, cancellationToken);
            if (format.IsFailure) return Result<Guid>.Failure(format.Error!);
        }
        var now = operation.UtcNow;
        var vehicle = new Vehicle(request.Type, plate, frame, request.Brand, request.Model, request.Color, now);
        await using var uploaded = new UploadedFileBatch(storage, logger);
        await vehicles.AddAsync(vehicle, cancellationToken);
        await ownerships.AddAsync(new VehicleOwnership(vehicle.Id, actor.Value.Id, now, actor.Value.Id), cancellationToken);
        var registration = new VehicleRegistration(vehicle.Id, actor.Value.Id, period.Id, now);
        await registrations.AddAsync(registration, cancellationToken);
        var key = await uploaded.UploadAsync(vehicle.Id, "verification", file, cancellationToken);
        var image = new VehicleVerificationImage(vehicle, key, file.FileName, file.ContentType, file.Bytes.LongLength, now);
        await evidence.AddVerificationImageAsync(image, cancellationToken);
        await operation.AuditAsync("VEHICLE_REGISTERED", vehicle.Id, null, new { Type = request.Type.ToString(), VerificationImageType = image.Type.ToString(), OwnerId = actor.Value.Id, PeriodId = period.Id }, cancellationToken);
        await operation.AuditAsync("VEHICLE_REGISTRATION_CREATED", vehicle.Id, null,
            new { registration.Id, registration.UserId, registration.AcademicPeriodId }, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        uploaded.MarkCommitStarted();
        await transaction.CommitAsync(cancellationToken);
        uploaded.MarkCommitted();
        return Result<Guid>.Success(vehicle.Id);
    }
}
public sealed class RenewVehicleRegistrationCommandHandler(VehicleOperationContext operation, IVehicleRegistrationRepository registrations,
    IVehicleEvidenceRepository evidence, IAcademicPeriodRepository periods, IUnitOfWork unitOfWork,
    FileUploadValidator fileValidator, IFileStorage storage, ILogger<UploadedFileBatch> logger)
    : IRequestHandler<RenewVehicleRegistrationCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(RenewVehicleRegistrationCommand request, CancellationToken cancellationToken)
    {
        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        var access = await operation.AuthorizedVehicleAsync(request.VehicleId, cancellationToken, ownerOnly: true, locked: true);
        if (access.IsFailure) return Result<Guid>.Failure(access.Error!);
        var vehicle = access.Value;
        if (vehicle.Status != VehicleStatus.ACTIVE) return Result<Guid>.Failure(VehicleErrors.Inactive);
        var actor = await operation.ActorAsync(cancellationToken);
        if (VehicleOperationContext.Eligibility(actor.Value, vehicle.Type) is { } eligibility) return Result<Guid>.Failure(eligibility);
        var period = await periods.GetActiveForShareAsync(cancellationToken);
        if (period is null) return Result<Guid>.Failure(AcademicPeriodErrors.NotActive);
        var previous = await registrations.GetByVehicleUserAndPeriodAsync(vehicle.Id, operation.ActorId, period.Id, cancellationToken);
        if (previous is not null) return Result<Guid>.Failure(previous.Status == VehicleRegistrationStatus.CANCELLED ? VehicleErrors.RegistrationCancelled : VehicleErrors.RegistrationAlreadyExists);
        var image = await evidence.GetVerificationImageAsync(vehicle.Id, cancellationToken);
        if (image is null) return Result<Guid>.Failure(VehicleVerificationErrors.Required);
        image.EnsureCompatible(vehicle.Type);
        var files = new List<ValidatedUpload>();
        foreach (var document in request.Documents)
        {
            var validation = await fileValidator.ValidateAsync(document.File, false, cancellationToken);
            if (validation.IsFailure) return Result<Guid>.Failure(validation.Error!);
            files.Add(validation.Value);
        }
        var now = operation.UtcNow;
        await using var uploaded = new UploadedFileBatch(storage, logger);
        for (var index = 0; index < request.Documents.Count; index++)
        {
            var file = files[index];
            var source = request.Documents[index];
            var key = await uploaded.UploadAsync(vehicle.Id, "documents", file, cancellationToken);
            await evidence.AddDocumentAsync(new VehicleDocument(vehicle.Id, source.Type, key, file.FileName, file.ContentType,
                file.Bytes.LongLength, now, source.DocumentNumber, source.IssuedOn, source.ExpiresOn), cancellationToken);
        }
        var registration = new VehicleRegistration(vehicle.Id, operation.ActorId, period.Id, now);
        await registrations.AddAsync(registration, cancellationToken);
        await operation.AuditAsync("VEHICLE_REGISTRATION_RENEWED", vehicle.Id, null, new { registration.Id, registration.AcademicPeriodId, registration.UserId }, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        uploaded.MarkCommitStarted();
        await transaction.CommitAsync(cancellationToken);
        uploaded.MarkCommitted();
        return Result<Guid>.Success(registration.Id);
    }
}
