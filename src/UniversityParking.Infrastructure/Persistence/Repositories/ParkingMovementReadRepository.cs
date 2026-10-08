using System.Data;
using Microsoft.EntityFrameworkCore;
using UniversityParking.Application.Common.Abstractions;
using UniversityParking.Application.Common.Pagination;
using UniversityParking.Application.Parking;
using UniversityParking.Domain.Parking;
using UniversityParking.Domain.Users;
using UniversityParking.Domain.Users.ValueObjects;
using UniversityParking.Domain.Vehicles;
using UniversityParking.Domain.Vehicles.ValueObjects;

namespace UniversityParking.Infrastructure.Persistence.Repositories;

public sealed class ParkingMovementReadRepository(AppDbContext context) : IParkingMovementReadRepository
{
    public async Task<ParkingMovementView?> GetByIdAsync(Guid id, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var row = await Rows(context.ParkingMovements.AsNoTracking().Where(x => x.Id == id)).SingleOrDefaultAsync(cancellationToken);
        return row is null ? null : Map(row, now);
    }
    public async Task<PagedResult<ParkingMovementView>> SearchAsync(ParkingMovementFilter filter, DateTimeOffset now, CancellationToken cancellationToken)
    {
        await using var snapshot = context.Database.CurrentTransaction is null ? await context.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead, cancellationToken) : null;
        var rows = Filter(filter);
        var total = await rows.LongCountAsync(cancellationToken);
        return await PageAsync(rows, filter, total, now, cancellationToken);
    }
    public async Task<VehiclesInsideView> GetInsideAsync(ParkingMovementFilter filter, DateTimeOffset now, CancellationToken cancellationToken)
    {
        await using var snapshot = context.Database.CurrentTransaction is null ? await context.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead, cancellationToken) : null;
        filter = filter with { Status = ParkingMovementStatus.OPEN };
        var rows = Filter(filter);
        var groups = await rows.GroupBy(x => x.Vehicle.Type).Select(x => new { Type = x.Key, Count = x.LongCount() }).ToListAsync(cancellationToken);
        var counts = groups.ToDictionary(x => x.Type, x => x.Count);
        var total = groups.Sum(x => x.Count);
        return new(await PageAsync(rows, filter, total, now, cancellationToken), new(total,
            counts.GetValueOrDefault(VehicleType.CAR), counts.GetValueOrDefault(VehicleType.MOTORCYCLE), counts.GetValueOrDefault(VehicleType.BICYCLE)));
    }
    private async Task<PagedResult<ParkingMovementView>> PageAsync(IQueryable<MovementRow> rows, ParkingMovementFilter filter,
        long total, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var offset = ((long)filter.Page - 1) * filter.PageSize;
        if (offset >= total) return new([], filter.Page, filter.PageSize, total);
        var items = await rows.OrderByDescending(x => x.Movement.CheckInAt).ThenBy(x => x.Movement.Id)
            .Skip((int)offset).Take(filter.PageSize).ToListAsync(cancellationToken);
        return new(items.Select(x => Map(x, now)), filter.Page, filter.PageSize, total);
    }
    private IQueryable<MovementRow> Filter(ParkingMovementFilter filter)
    {
        var movements = context.ParkingMovements.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var escaped = filter.Search.Trim().Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_");
            var pattern = "%" + escaped + "%";
            movements = context.ParkingMovements.FromSqlInterpolated($"SELECT pm.* FROM parking_movements pm JOIN users u ON u.id = pm.user_id JOIN vehicles v ON v.id = pm.vehicle_id WHERE u.identification_number ILIKE {pattern} ESCAPE '\\' OR u.full_name ILIKE {pattern} ESCAPE '\\' OR v.plate ILIKE {pattern} ESCAPE '\\' OR v.frame_number ILIKE {pattern} ESCAPE '\\'").AsNoTracking();
        }
        if (filter.UserId.HasValue) movements = movements.Where(x => x.UserId == filter.UserId.Value);
        if (filter.VehicleId.HasValue) movements = movements.Where(x => x.VehicleId == filter.VehicleId.Value);
        if (filter.ParkingLotId.HasValue) movements = movements.Where(x => x.ParkingLotId == filter.ParkingLotId.Value);
        if (filter.Status.HasValue) movements = movements.Where(x => x.Status == filter.Status.Value);
        if (filter.FromUtc.HasValue) movements = movements.Where(x => x.CheckInAt >= filter.FromUtc.Value);
        if (filter.UntilUtc.HasValue) movements = movements.Where(x => x.CheckInAt < filter.UntilUtc.Value);
        var rows = Rows(movements);
        if (filter.VehicleType.HasValue) rows = rows.Where(x => x.Vehicle.Type == filter.VehicleType.Value);
        if (!string.IsNullOrWhiteSpace(filter.IdentificationNumber))
        {
            var number = new IdentificationNumber(filter.IdentificationNumber);
            rows = rows.Where(x => x.User.IdentificationNumber == number);
        }
        if (!string.IsNullOrWhiteSpace(filter.Plate))
        {
            var plate = new VehiclePlate(filter.Plate);
            rows = rows.Where(x => x.Vehicle.Plate == plate);
        }
        if (!string.IsNullOrWhiteSpace(filter.FrameNumber))
        {
            var frame = new FrameNumber(filter.FrameNumber);
            rows = rows.Where(x => x.Vehicle.FrameNumber == frame);
        }
        return rows;
    }
    private IQueryable<MovementRow> Rows(IQueryable<ParkingMovement> movements) =>
        from movement in movements
        join user in context.Users.AsNoTracking() on movement.UserId equals user.Id
        join vehicle in context.Vehicles.AsNoTracking() on movement.VehicleId equals vehicle.Id
        join lot in context.ParkingLots.AsNoTracking() on movement.ParkingLotId equals lot.Id
        join zone in context.ParkingZones.AsNoTracking() on movement.ParkingZoneId equals zone.Id
        select new MovementRow { Movement = movement, User = user, Vehicle = vehicle, Lot = lot, Zone = zone };
    private static ParkingMovementView Map(MovementRow row, DateTimeOffset now) => new(row.Movement.Id, row.User.Id, row.User.FullName,
        row.Vehicle.Id, row.Vehicle.Type, row.Vehicle.Plate?.Value ?? row.Vehicle.FrameNumber!.Value, row.Lot.Id, row.Lot.Name, row.Zone.Id, row.Zone.Name,
        row.Movement.CheckInAt, row.Movement.CheckInGuardId, row.Movement.CheckOutAt, row.Movement.CheckOutGuardId, row.Movement.Status,
        row.Movement.GetDuration(now < row.Movement.CheckInAt ? row.Movement.CheckInAt : now));
    private sealed class MovementRow
    {
        public ParkingMovement Movement { get; init; } = null!;
        public User User { get; init; } = null!;
        public Vehicle Vehicle { get; init; } = null!;
        public ParkingLot Lot { get; init; } = null!;
        public ParkingZone Zone { get; init; } = null!;
    }
}
