using MediatR;
using UniversityParking.Application.Common.Abstractions;
using UniversityParking.Application.Common.Authorization;
using UniversityParking.Application.Common.Errors;
using UniversityParking.Application.Common.Pagination;
using UniversityParking.Application.Common.Results;
using UniversityParking.Domain.Parking;
using UniversityParking.Domain.Users;
using UniversityParking.Domain.Vehicles;

namespace UniversityParking.Application.ParkingLots;

public sealed class CreateParkingLotCommandHandler(AdministrationOperationContext operation, IParkingLotRepository lots, IUnitOfWork unitOfWork)
    : IRequestHandler<CreateParkingLotCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(CreateParkingLotCommand request, CancellationToken cancellationToken)
    {
        if (await operation.CheckAccessAsync([RoleCodes.Admin], cancellationToken) is { } error) return Result<Guid>.Failure(error);
        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        await operation.LockActorAsync(cancellationToken);
        var name = request.Name.Trim();
        var campus = request.Campus.Trim();
        if (await lots.ExistsByNameAndCampusAsync(name, campus, null, cancellationToken)) return Result<Guid>.Failure(ParkingErrors.LotAlreadyExists);
        var now = operation.UtcNow;
        var lot = new ParkingLot(name, campus, request.OpeningTime, request.ClosingTime, now);
        await lots.AddAsync(lot, cancellationToken);
        foreach (var (type, zoneName) in new[] { (VehicleType.CAR, "Zona de carros"), (VehicleType.MOTORCYCLE, "Zona de motos"), (VehicleType.BICYCLE, "Zona de bicicletas") })
            await lots.AddZoneAsync(new ParkingZone(lot.Id, zoneName, type, now), cancellationToken);
        await operation.AuditAsync("PARKING_LOT_CREATED", "ParkingLot", lot.Id, null,
            new { lot.Name, lot.Campus, lot.OpeningTime, lot.ClosingTime, Status = "ACTIVE" }, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return Result<Guid>.Success(lot.Id);
    }
}
public sealed class UpdateParkingLotCommandHandler(AdministrationOperationContext operation, IParkingLotRepository lots, IUnitOfWork unitOfWork)
    : IRequestHandler<UpdateParkingLotCommand, Result>
{
    public async Task<Result> Handle(UpdateParkingLotCommand request, CancellationToken cancellationToken)
    {
        if (await operation.CheckAccessAsync([RoleCodes.Admin], cancellationToken) is { } error) return Result.Failure(error);
        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        await operation.LockActorAsync(cancellationToken);
        var lot = await lots.GetByIdForUpdateAsync(request.ParkingLotId, cancellationToken);
        if (lot is null) return Result.Failure(ParkingErrors.LotNotFound);
        if (await lots.ExistsByNameAndCampusAsync(request.Name.Trim(), request.Campus.Trim(), lot.Id, cancellationToken)) return Result.Failure(ParkingErrors.LotAlreadyExists);
        var before = new { lot.Name, lot.Campus, lot.OpeningTime, lot.ClosingTime };
        lot.Update(request.Name, request.Campus, request.OpeningTime, request.ClosingTime, operation.UtcNow);
        await operation.AuditAsync("PARKING_LOT_UPDATED", "ParkingLot", lot.Id, before, new { lot.Name, lot.Campus, lot.OpeningTime, lot.ClosingTime }, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return Result.Success();
    }
}
public sealed class ActivateParkingLotCommandHandler(AdministrationOperationContext operation, IParkingLotRepository lots, IUnitOfWork unitOfWork)
    : IRequestHandler<ActivateParkingLotCommand, Result>
{
    public async Task<Result> Handle(ActivateParkingLotCommand request, CancellationToken cancellationToken)
    {
        if (await operation.CheckAccessAsync([RoleCodes.Admin], cancellationToken) is { } error) return Result.Failure(error);
        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        await operation.LockActorAsync(cancellationToken);
        var lot = await lots.GetByIdForUpdateAsync(request.ParkingLotId, cancellationToken);
        if (lot is null) return Result.Failure(ParkingErrors.LotNotFound);
        if (lot.Status == ParkingLotStatus.ACTIVE) return Result.Success();
        lot.Activate(operation.UtcNow);
        await operation.AuditAsync("PARKING_LOT_ACTIVATED", "ParkingLot", lot.Id, new { Status = "INACTIVE" }, new { Status = "ACTIVE" }, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return Result.Success();
    }
}
public sealed class DeactivateParkingLotCommandHandler(AdministrationOperationContext operation, IParkingLotRepository lots, IParkingMovementRepository movements, IUnitOfWork unitOfWork)
    : IRequestHandler<DeactivateParkingLotCommand, Result>
{
    public async Task<Result> Handle(DeactivateParkingLotCommand request, CancellationToken cancellationToken)
    {
        if (await operation.CheckAccessAsync([RoleCodes.Admin], cancellationToken) is { } error) return Result.Failure(error);
        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        await operation.LockActorAsync(cancellationToken);
        var lot = await lots.GetByIdForUpdateAsync(request.ParkingLotId, cancellationToken);
        if (lot is null) return Result.Failure(ParkingErrors.LotNotFound);
        if (lot.Status == ParkingLotStatus.INACTIVE) return Result.Success();
        if (await movements.ExistsOpenByParkingLotIdAsync(lot.Id, cancellationToken)) return Result.Failure(ParkingErrors.LotHasOpenMovements);
        lot.Deactivate(operation.UtcNow);
        await operation.AuditAsync("PARKING_LOT_DEACTIVATED", "ParkingLot", lot.Id, new { Status = "ACTIVE" }, new { Status = "INACTIVE" }, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return Result.Success();
    }
}
public sealed class GetParkingLotsQueryHandler(AdministrationOperationContext operation, IParkingLotRepository lots)
    : IRequestHandler<GetParkingLotsQuery, Result<PagedResult<ParkingLotView>>>
{
    public async Task<Result<PagedResult<ParkingLotView>>> Handle(GetParkingLotsQuery request, CancellationToken cancellationToken)
    {
        if (await operation.CheckAccessAsync([RoleCodes.Admin], cancellationToken) is { } error) return Result<PagedResult<ParkingLotView>>.Failure(error);
        return Result<PagedResult<ParkingLotView>>.Success(await lots.SearchAsync(request.Status, new(request.Page, request.PageSize), cancellationToken));
    }
}
public sealed class GetActiveParkingLotsQueryHandler(AdministrationOperationContext operation, IParkingLotRepository lots)
    : IRequestHandler<GetActiveParkingLotsQuery, Result<PagedResult<ParkingLotView>>>
{
    public async Task<Result<PagedResult<ParkingLotView>>> Handle(GetActiveParkingLotsQuery request, CancellationToken cancellationToken)
    {
        if (await operation.CheckAccessAsync([RoleCodes.Guard, RoleCodes.Admin], cancellationToken) is { } error) return Result<PagedResult<ParkingLotView>>.Failure(error);
        return Result<PagedResult<ParkingLotView>>.Success(await lots.SearchAsync(ParkingLotStatus.ACTIVE, new(request.Page, request.PageSize), cancellationToken));
    }
}

