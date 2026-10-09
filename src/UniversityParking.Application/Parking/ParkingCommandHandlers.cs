using MediatR;
using UniversityParking.Application.Common.Abstractions;
using UniversityParking.Application.Common.Authorization;
using UniversityParking.Application.Common.Errors;
using UniversityParking.Application.Common.Results;
using UniversityParking.Domain.Parking;
using UniversityParking.Domain.Users;
using UniversityParking.Domain.Vehicles;

namespace UniversityParking.Application.Parking;

public sealed class CheckInVehicleCommandHandler(AdministrationOperationContext operation, ICurrentUser actor,
    IUserRepository users, IVehicleRepository vehicles, IVehicleOwnershipRepository ownerships, IAcademicPeriodRepository periods,
    IVehicleRegistrationRepository registrations, IParkingLotRepository lots, IParkingMovementRepository movements,
    IParkingMovementReadRepository reads, IParkingTimeZone timeZone, IClock clock, IUnitOfWork unitOfWork, IVehicleEvidenceRepository evidence)
    : IRequestHandler<CheckInVehicleCommand, Result<ParkingMovementView>>
{
    public async Task<Result<ParkingMovementView>> Handle(CheckInVehicleCommand request, CancellationToken cancellationToken)
    {
        if (await operation.CheckAccessAsync([RoleCodes.Guard], cancellationToken) is { } permission) return Result<ParkingMovementView>.Failure(permission);
        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        User? user = null;
        // Lock both FK users in the same order as account and ownership operations.
        foreach (var id in new[] { actor.UserId!.Value, request.UserId }.Distinct().Order())
        {
            var locked = await users.GetByIdForUpdateAsync(id, cancellationToken);
            if (id == request.UserId) user = locked;
        }
        if (await operation.CheckAccessAsync([RoleCodes.Guard], cancellationToken) is { } currentPermission) return Result<ParkingMovementView>.Failure(currentPermission);
        if (user is null) return Result<ParkingMovementView>.Failure(UserErrors.NotFound);
        if (user.Status != UserStatus.ACTIVE) return Result<ParkingMovementView>.Failure(UserErrors.Inactive);
        var vehicle = await vehicles.GetByIdForUpdateAsync(request.VehicleId, cancellationToken);
        if (vehicle is null) return Result<ParkingMovementView>.Failure(VehicleErrors.NotFound);
        if (vehicle.Status != VehicleStatus.ACTIVE) return Result<ParkingMovementView>.Failure(VehicleErrors.Inactive);
        var ownership = await ownerships.GetCurrentByVehicleIdAsync(vehicle.Id, cancellationToken);
        if (ownership is null) return Result<ParkingMovementView>.Failure(VehicleErrors.OwnershipNotFound);
        if (ownership.UserId != user.Id) return Result<ParkingMovementView>.Failure(VehicleErrors.NotOwnedByUser);
        if (user.UserType == InstitutionalUserType.STUDENT && vehicle.Type == VehicleType.CAR) return Result<ParkingMovementView>.Failure(VehicleErrors.StudentCannotRegisterCar);
        var period = await periods.GetActiveForShareAsync(cancellationToken);
        if (period is null) return Result<ParkingMovementView>.Failure(AcademicPeriodErrors.NotActive);
        var registration = await registrations.GetByVehicleUserAndPeriodAsync(vehicle.Id, user.Id, period.Id, cancellationToken);
        if (registration is null) return Result<ParkingMovementView>.Failure(VehicleErrors.RegistrationRequired);
        if (registration.Status != VehicleRegistrationStatus.ACTIVE) return Result<ParkingMovementView>.Failure(VehicleErrors.RegistrationCancelled);
        var lot = await lots.GetByIdForUpdateAsync(request.ParkingLotId, cancellationToken);
        if (lot is null) return Result<ParkingMovementView>.Failure(ParkingErrors.LotNotFound);
        if (lot.Status != ParkingLotStatus.ACTIVE) return Result<ParkingMovementView>.Failure(ParkingErrors.LotInactive);
        var zone = await lots.GetZoneForVehicleTypeAsync(lot.Id, vehicle.Type, cancellationToken);
        if (zone is null || zone.Status != ParkingZoneStatus.ACTIVE) return Result<ParkingMovementView>.Failure(ParkingErrors.ZoneUnavailable);
        var now = clock.UtcNow;
        if (!lot.AllowsEntryAt(timeZone.GetLocalTime(now))) return Result<ParkingMovementView>.Failure(ParkingErrors.LotClosed);
        if (await movements.ExistsOpenByVehicleIdAsync(vehicle.Id, cancellationToken)) return Result<ParkingMovementView>.Failure(ParkingErrors.VehicleAlreadyInside);
        if (await movements.ExistsOpenByUserIdAsync(user.Id, cancellationToken)) return Result<ParkingMovementView>.Failure(ParkingErrors.UserAlreadyHasVehicleInside);
        var image = await evidence.GetVerificationImageAsync(vehicle.Id, cancellationToken);
        if (image is null || image.Type != VehicleVerificationImage.ForVehicle(vehicle.Type))
            return Result<ParkingMovementView>.Failure(new("VEHICLE_VERIFICATION_REQUIRED", "El vehículo no tiene evidencia de verificación registrada.", ErrorType.Conflict));
        if (request.MovementId is { } expectedId && await movements.GetByIdAsync(expectedId, cancellationToken) is not null)
            return Result<ParkingMovementView>.Failure(new("PARKING_MOVEMENT_ID_ALREADY_USED", "El identificador de este intento ya fue utilizado. Consulta el estado actual.", ErrorType.Conflict));
        var movement = new ParkingMovement(user.Id, vehicle.Id, lot.Id, zone.Id, now, actor.UserId!.Value, request.MovementId);
        await movements.AddAsync(movement, cancellationToken);
        await operation.AuditAsync("PARKING_CHECK_IN", "ParkingMovement", movement.Id, null,
            new { movement.UserId, movement.VehicleId, movement.ParkingLotId, movement.ParkingZoneId, movement.CheckInAt, movement.CheckInGuardId }, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        var view = await reads.GetByIdAsync(movement.Id, now, cancellationToken)
            ?? throw new InvalidOperationException("No fue posible recuperar el movimiento persistido.");
        await transaction.CommitAsync(cancellationToken);
        return Result<ParkingMovementView>.Success(view);
    }
}
public sealed class CheckOutVehicleCommandHandler(AdministrationOperationContext operation, ICurrentUser actor, IUserRepository users, IVehicleRepository vehicles,
    IParkingMovementRepository movements, IParkingMovementReadRepository reads, IClock clock, IUnitOfWork unitOfWork)
    : IRequestHandler<CheckOutVehicleCommand, Result<ParkingMovementView>>
{
    public async Task<Result<ParkingMovementView>> Handle(CheckOutVehicleCommand request, CancellationToken cancellationToken)
    {
        if (await operation.CheckAccessAsync([RoleCodes.Guard], cancellationToken) is { } permission) return Result<ParkingMovementView>.Failure(permission);
        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        if (await vehicles.GetByIdAsync(request.VehicleId, cancellationToken) is null) return Result<ParkingMovementView>.Failure(VehicleErrors.NotFound);
        var observed = await movements.GetByIdAsync(request.MovementId, cancellationToken);
        if (observed is null || observed.Status != ParkingMovementStatus.OPEN || observed.VehicleId != request.VehicleId)
            return Result<ParkingMovementView>.Failure(ParkingErrors.VehicleNotInside);
        foreach (var id in new[] { actor.UserId!.Value, observed.UserId }.Distinct().Order())
            await users.GetByIdForUpdateAsync(id, cancellationToken);
        if (await operation.CheckAccessAsync([RoleCodes.Guard], cancellationToken) is { } currentPermission) return Result<ParkingMovementView>.Failure(currentPermission);
        if (await vehicles.GetByIdForUpdateAsync(request.VehicleId, cancellationToken) is null) return Result<ParkingMovementView>.Failure(VehicleErrors.NotFound);
        var movement = await movements.GetByIdForUpdateAsync(request.MovementId, cancellationToken);
        if (movement is null || movement.Status != ParkingMovementStatus.OPEN || movement.VehicleId != request.VehicleId)
            return Result<ParkingMovementView>.Failure(ParkingErrors.VehicleNotInside);
        var now = clock.UtcNow;
        movement.Close(now, actor.UserId!.Value);
        await operation.AuditAsync("PARKING_CHECK_OUT", "ParkingMovement", movement.Id, new { Status = "OPEN" },
            new { Status = "CLOSED", movement.CheckOutAt, movement.CheckOutGuardId }, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        var view = await reads.GetByIdAsync(movement.Id, now, cancellationToken)
            ?? throw new InvalidOperationException("No fue posible recuperar el movimiento persistido.");
        await transaction.CommitAsync(cancellationToken);
        return Result<ParkingMovementView>.Success(view);
    }
}
