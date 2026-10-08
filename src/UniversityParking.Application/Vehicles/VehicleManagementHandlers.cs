using MediatR;
using UniversityParking.Application.Common.Abstractions;
using UniversityParking.Application.Common.Errors;
using UniversityParking.Application.Common.Results;
using UniversityParking.Domain.Users;
using UniversityParking.Domain.Users.ValueObjects;
using UniversityParking.Domain.Vehicles;
using UniversityParking.Domain.Vehicles.ValueObjects;

namespace UniversityParking.Application.Vehicles;

public sealed class UpdateVehicleCommandHandler(VehicleOperationContext operation, IUnitOfWork unitOfWork)
    : IRequestHandler<UpdateVehicleCommand, Result>
{
    public async Task<Result> Handle(UpdateVehicleCommand request, CancellationToken cancellationToken)
    {
        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        var access = await operation.AuthorizedVehicleAsync(request.VehicleId, cancellationToken, locked: true);
        if (access.IsFailure) return Result.Failure(access.Error!);
        var vehicle = access.Value;
        var before = new { vehicle.Brand, vehicle.Model, vehicle.Color };
        vehicle.UpdateDescription(request.Brand, request.Model, request.Color, operation.UtcNow);
        await operation.AuditAsync("VEHICLE_UPDATED", vehicle.Id, before, new { vehicle.Brand, vehicle.Model, vehicle.Color }, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return Result.Success();
    }
}
public sealed class ActivateVehicleCommandHandler(VehicleOperationContext operation, IVehicleOwnershipRepository ownerships,
    IUserRepository users, IUnitOfWork unitOfWork) : IRequestHandler<ActivateVehicleCommand, Result>
{
    public async Task<Result> Handle(ActivateVehicleCommand request, CancellationToken cancellationToken)
    {
        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        var actor = await operation.ActorAsync(cancellationToken);
        if (actor.IsFailure) return Result.Failure(actor.Error!);
        var observedOwnership = await ownerships.GetCurrentByVehicleIdAsync(request.VehicleId, cancellationToken);
        var userIds = observedOwnership is null ? new[] { operation.ActorId } : new[] { operation.ActorId, observedOwnership.UserId };
        foreach (var id in userIds.Distinct().Order()) await users.GetByIdForUpdateAsync(id, cancellationToken);
        var access = await operation.AuthorizedVehicleAsync(request.VehicleId, cancellationToken, locked: true);
        if (access.IsFailure) return Result.Failure(access.Error!);
        var ownership = await ownerships.GetCurrentByVehicleIdAsync(request.VehicleId, cancellationToken);
        if (ownership is null) return Result.Failure(VehicleErrors.OwnershipNotFound);
        if (observedOwnership?.UserId != ownership.UserId) return Result.Failure(VehicleErrors.InvalidOwner);
        var owner = await users.GetByIdAsync(ownership.UserId, cancellationToken);
        if (owner is null) return Result.Failure(VehicleErrors.InvalidOwner);
        if (VehicleOperationContext.Eligibility(owner, access.Value.Type) is { } error) return Result.Failure(error);
        if (access.Value.Status == VehicleStatus.ACTIVE) return Result.Success();
        access.Value.Activate(operation.UtcNow);
        await operation.AuditAsync("VEHICLE_ACTIVATED", request.VehicleId, new { Status = "INACTIVE" }, new { Status = "ACTIVE" }, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return Result.Success();
    }
}
public sealed class DeactivateVehicleCommandHandler(VehicleOperationContext operation, IParkingMovementRepository movements,
    IUnitOfWork unitOfWork) : IRequestHandler<DeactivateVehicleCommand, Result>
{
    public async Task<Result> Handle(DeactivateVehicleCommand request, CancellationToken cancellationToken)
    {
        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        var access = await operation.AuthorizedVehicleAsync(request.VehicleId, cancellationToken, locked: true);
        if (access.IsFailure) return Result.Failure(access.Error!);
        if (access.Value.Status == VehicleStatus.INACTIVE) return Result.Success();
        if (await movements.ExistsOpenByVehicleIdAsync(request.VehicleId, cancellationToken)) return Result.Failure(VehicleErrors.HasOpenParkingMovement);
        access.Value.Deactivate(operation.UtcNow);
        await operation.AuditAsync("VEHICLE_DEACTIVATED", request.VehicleId, new { Status = "ACTIVE" }, new { Status = "INACTIVE" }, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return Result.Success();
    }
}
public sealed class CorrectVehicleIdentifierCommandHandler(VehicleOperationContext operation, IVehicleRepository vehicles,
    IUnitOfWork unitOfWork) : IRequestHandler<CorrectVehicleIdentifierCommand, Result>
{
    public async Task<Result> Handle(CorrectVehicleIdentifierCommand request, CancellationToken cancellationToken)
    {
        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        var access = await operation.AuthorizedVehicleAsync(request.VehicleId, cancellationToken, adminOnly: true, locked: true);
        if (access.IsFailure) return Result.Failure(access.Error!);
        var vehicle = access.Value;
        var duplicate = vehicle.Type == VehicleType.BICYCLE ? await vehicles.GetByFrameNumberAsync(new FrameNumber(request.Identifier), cancellationToken) :
            await vehicles.GetByPlateAsync(new VehiclePlate(request.Identifier), cancellationToken);
        if (duplicate is not null && duplicate.Id != vehicle.Id) return Result.Failure(VehicleErrors.IdentifierAlreadyExists);
        var oldIdentifier = vehicle.Plate?.Value ?? vehicle.FrameNumber!.Value;
        vehicle.CorrectIdentifier(request.Identifier, operation.UtcNow);
        await operation.AuditAsync("VEHICLE_IDENTIFIER_CORRECTED", vehicle.Id, new { Identifier = oldIdentifier },
            new { Identifier = vehicle.Plate?.Value ?? vehicle.FrameNumber!.Value, request.Reason }, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return Result.Success();
    }
}
public sealed class TransferVehicleCommandHandler(VehicleOperationContext operation, IUserRepository users,
    IVehicleOwnershipRepository ownerships, IVehicleRegistrationRepository registrations, IParkingMovementRepository movements,
    IAcademicPeriodRepository periods, IUnitOfWork unitOfWork) : IRequestHandler<TransferVehicleCommand, Result>
{
    public async Task<Result> Handle(TransferVehicleCommand request, CancellationToken cancellationToken)
    {
        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        var actor = await operation.ActorAsync(cancellationToken);
        if (actor.IsFailure) return Result.Failure(actor.Error!);
        if (!await operation.HasRoleAsync(RoleCodes.Admin, cancellationToken)) return Result.Failure(CommonErrors.Forbidden);
        var newOwner = await users.GetByIdentificationNumberAsync(new IdentificationNumber(request.NewOwnerIdentificationNumber), cancellationToken);
        if (newOwner is null) return Result.Failure(UserErrors.NotFound);
        foreach (var id in new[] { operation.ActorId, newOwner.Id }.Distinct().Order())
            await users.GetByIdForUpdateAsync(id, cancellationToken);
        var access = await operation.AuthorizedVehicleAsync(request.VehicleId, cancellationToken, adminOnly: true, locked: true);
        if (access.IsFailure) return Result.Failure(access.Error!);
        var current = await ownerships.GetCurrentByVehicleIdAsync(request.VehicleId, cancellationToken);
        if (current is null) return Result.Failure(VehicleErrors.OwnershipNotFound);
        if (current.UserId == newOwner.Id) return Result.Failure(VehicleErrors.InvalidOwner);
        if (VehicleOperationContext.Eligibility(newOwner, access.Value.Type) is { } error) return Result.Failure(error);
        if (await movements.ExistsOpenByVehicleIdAsync(request.VehicleId, cancellationToken)) return Result.Failure(VehicleErrors.HasOpenParkingMovement);
        var now = operation.UtcNow;
        current.Close(now, request.Reason);
        var period = await periods.GetActiveForShareAsync(cancellationToken);
        if (period is not null)
        {
            var registration = await registrations.GetActiveForVehicleUserAndPeriodAsync(request.VehicleId, current.UserId, period.Id, cancellationToken);
            if (registration is not null)
            {
                registration.Cancel(now, operation.ActorId, "OWNERSHIP_TRANSFERRED");
                await operation.AuditAsync("VEHICLE_REGISTRATION_CANCELLED", request.VehicleId, new { Status = "ACTIVE" },
                    new { registration.Id, Status = "CANCELLED", Reason = "OWNERSHIP_TRANSFERRED" }, cancellationToken);
            }
        }
        // Free the filtered unique ownership index before inserting the new current owner.
        // Both saves remain inside this transaction, including the audit.
        await unitOfWork.SaveChangesAsync(cancellationToken);
        await ownerships.AddAsync(new VehicleOwnership(request.VehicleId, newOwner.Id, now, operation.ActorId, request.Reason), cancellationToken);
        await operation.AuditAsync("VEHICLE_TRANSFERRED", request.VehicleId, new { OwnerId = current.UserId },
            new { OwnerId = newOwner.Id, request.Reason }, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return Result.Success();
    }
}

