using MediatR;
using FluentValidation;
using Microsoft.Extensions.Logging;
using UniversityParking.Application.Common.Abstractions;
using UniversityParking.Application.Common.Errors;
using UniversityParking.Application.Common.Results;
using UniversityParking.Application.Files;
using UniversityParking.Domain.Users;
using UniversityParking.Domain.Vehicles;

namespace UniversityParking.Application.Vehicles;

public static class VehicleVerificationErrors
{
    public static Error Required { get; } = new("VEHICLE_VERIFICATION_IMAGE_REQUIRED",
        "Evidencia de verificación pendiente. Actualiza la imagen antes de renovar.", ErrorType.Conflict);
}
public sealed class UpdateVehicleVerificationImageCommandValidator : AbstractValidator<UpdateVehicleVerificationImageCommand>
{
    public UpdateVehicleVerificationImageCommandValidator()
    { RuleFor(x => x.VehicleId).NotEmpty(); RuleFor(x => x.VerificationImage).NotNull(); }
}
public sealed class UpdateVehicleVerificationImageCommandHandler(VehicleOperationContext operation,
    IVehicleEvidenceRepository evidence, IUnitOfWork unitOfWork, FileUploadValidator validator, IFileStorage storage,
    ILogger<UploadedFileBatch> logger) : IRequestHandler<UpdateVehicleVerificationImageCommand, Result>
{
    public async Task<Result> Handle(UpdateVehicleVerificationImageCommand request, CancellationToken cancellationToken)
    {
        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        var access = await operation.AuthorizedVehicleAsync(request.VehicleId, cancellationToken, locked: true);
        if (access.IsFailure) return Result.Failure(access.Error!);
        if (await operation.HasRoleAsync(RoleCodes.Guard, cancellationToken) && !await operation.HasRoleAsync(RoleCodes.Admin, cancellationToken))
            return Result.Failure(CommonErrors.Forbidden);
        if (request.VerificationImage is null) return Result.Failure(VehicleVerificationErrors.Required);
        var validated = await validator.ValidateAsync(request.VerificationImage, true, cancellationToken);
        if (validated.IsFailure) return Result.Failure(validated.Error!);
        var vehicle = access.Value;
        var image = await evidence.GetVerificationImageAsync(vehicle.Id, cancellationToken);
        var oldKey = image?.StorageKey;
        var oldValue = image is null ? null : new { image.Id, Type = image.Type.ToString(), image.OriginalFileName };
        await using var uploaded = new UploadedFileBatch(storage, logger);
        var file = validated.Value;
        var key = await uploaded.UploadAsync(vehicle.Id, "verification", file, cancellationToken);
        if (image is null)
        {
            image = new(vehicle, key, file.FileName, file.ContentType, file.Bytes.LongLength, operation.UtcNow);
            await evidence.AddVerificationImageAsync(image, cancellationToken);
        }
        else image.Replace(vehicle, key, file.FileName, file.ContentType, file.Bytes.LongLength, operation.UtcNow);
        await operation.AuditAsync("VEHICLE_VERIFICATION_IMAGE_UPDATED", vehicle.Id, oldValue,
            new { image.Id, Type = image.Type.ToString(), image.OriginalFileName }, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        uploaded.MarkCommitStarted(); await transaction.CommitAsync(cancellationToken); uploaded.MarkCommitted();
        if (oldKey is not null && oldKey != key)
        {
            try { await storage.DeleteAsync(oldKey, CancellationToken.None); }
            catch (Exception exception) { logger.LogWarning("Previous verification image cleanup deferred. ExceptionType {ExceptionType}", exception.GetType().Name); }
        }
        return Result.Success();
    }
}
public sealed class GetVehicleVerificationImageContentQueryHandler(VehicleOperationContext operation,
    IVehicleEvidenceRepository evidence, IFileStorage storage) : IRequestHandler<GetVehicleVerificationImageContentQuery, Result<FileContent>>
{
    public async Task<Result<FileContent>> Handle(GetVehicleVerificationImageContentQuery request, CancellationToken cancellationToken)
    {
        var access = await operation.AuthorizedVehicleAsync(request.VehicleId, cancellationToken, photoAccess: true);
        if (access.IsFailure) return Result<FileContent>.Failure(access.Error!);
        var image = await evidence.GetVerificationImageAsync(request.VehicleId, cancellationToken);
        if (image is null) return Result<FileContent>.Failure(VehicleErrors.NotFound);
        image.EnsureCompatible(access.Value.Type);
        return await VehicleContent.ReadAsync(storage, image.StorageKey, image.ContentType, image.OriginalFileName, cancellationToken);
    }
}
