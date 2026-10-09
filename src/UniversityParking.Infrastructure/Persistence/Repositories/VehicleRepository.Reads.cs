using Microsoft.EntityFrameworkCore;
using UniversityParking.Application.Common.Pagination;
using UniversityParking.Application.Vehicles;
using UniversityParking.Domain.AcademicPeriods;
using UniversityParking.Domain.Parking;
using UniversityParking.Domain.Users;
using UniversityParking.Domain.Users.ValueObjects;
using UniversityParking.Domain.Vehicles;

namespace UniversityParking.Infrastructure.Persistence.Repositories;

public sealed partial class VehicleRepository
{
    public async Task<VehicleView?> GetViewAsync(Guid id, CancellationToken cancellationToken) =>
        (await ReadRowsAsync(context.Vehicles.AsNoTracking().Where(x => x.Id == id), null, cancellationToken)).SingleOrDefault();
    public Task<IReadOnlyList<VehicleView>> GetByCurrentOwnerAsync(Guid userId, CancellationToken cancellationToken) =>
        ReadRowsAsync(context.Vehicles.AsNoTracking().Where(x => context.VehicleOwnerships.Any(o => o.VehicleId == x.Id && o.UserId == userId && o.EndAt == null)), null, cancellationToken);
    public async Task<PagedResult<VehicleView>> SearchAsync(GetVehiclesQuery query, CancellationToken cancellationToken)
    {
        var selection = context.Vehicles.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var escaped = query.Search.Trim().Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_");
            var pattern = "%" + escaped + "%";
            selection = context.Vehicles.FromSqlInterpolated($"SELECT * FROM vehicles WHERE plate ILIKE {pattern} ESCAPE '\\' OR frame_number ILIKE {pattern} ESCAPE '\\' OR brand ILIKE {pattern} ESCAPE '\\' OR model ILIKE {pattern} ESCAPE '\\'").AsNoTracking();
        }
        if (query.Type.HasValue) selection = selection.Where(x => x.Type == query.Type.Value);
        if (query.Status.HasValue) selection = selection.Where(x => x.Status == query.Status.Value);
        if (!string.IsNullOrWhiteSpace(query.OwnerIdentificationNumber))
        {
            var identification = new IdentificationNumber(query.OwnerIdentificationNumber);
            selection = selection.Where(vehicle => (from ownership in context.VehicleOwnerships
                join user in context.Users on ownership.UserId equals user.Id
                where ownership.VehicleId == vehicle.Id && ownership.EndAt == null && user.IdentificationNumber == identification
                select user).Any());
        }
        var rows = await RowQueryAsync(selection, cancellationToken);
        if (query.RegistrationState.HasValue) rows = rows.Where(x => x.State == query.RegistrationState.Value);
        var total = await rows.LongCountAsync(cancellationToken);
        var offset = ((long)query.Page - 1) * query.PageSize;
        var items = offset >= total ? [] : await rows.OrderBy(x => x.Vehicle.CreatedAt).ThenBy(x => x.Vehicle.Id)
            .Skip((int)offset).Take(query.PageSize).ToListAsync(cancellationToken);
        return new PagedResult<VehicleView>(items.Select(Map), query.Page, query.PageSize, total);
    }
    private async Task<IReadOnlyList<VehicleView>> ReadRowsAsync(IQueryable<Vehicle> selection, RegistrationState? state, CancellationToken cancellationToken)
    {
        var rows = await RowQueryAsync(selection, cancellationToken);
        if (state.HasValue) rows = rows.Where(x => x.State == state.Value);
        return (await rows.OrderBy(x => x.Vehicle.CreatedAt).ThenBy(x => x.Vehicle.Id).ToListAsync(cancellationToken)).Select(Map).ToArray();
    }
    private async Task<IQueryable<VehicleRow>> RowQueryAsync(IQueryable<Vehicle> selection, CancellationToken cancellationToken)
    {
        var activeId = await context.AcademicPeriods.Where(x => x.Status == AcademicPeriodStatus.ACTIVE)
            .Select(x => (Guid?)x.Id).SingleOrDefaultAsync(cancellationToken);
        return from vehicle in selection
            from ownership in context.VehicleOwnerships.AsNoTracking().Where(x => x.VehicleId == vehicle.Id && x.EndAt == null).DefaultIfEmpty()
            from owner in context.Users.AsNoTracking().Where(x => ownership != null && x.Id == ownership.UserId).DefaultIfEmpty()
            let registration = context.VehicleRegistrations.AsNoTracking().Where(x => x.VehicleId == vehicle.Id && ownership != null && x.UserId == ownership.UserId)
                .OrderByDescending(x => x.AcademicPeriodId == activeId).ThenByDescending(x => x.RegisteredAt).ThenBy(x => x.Id).FirstOrDefault()
            select new VehicleRow { Vehicle = vehicle, OwnerId = owner == null ? null : owner.Id, OwnerName = owner == null ? null : owner.FullName, State =
                registration == null ? RegistrationState.NONE : registration.Status == VehicleRegistrationStatus.CANCELLED ? RegistrationState.CANCELLED :
                    registration.AcademicPeriodId == activeId ? RegistrationState.ACTIVE : RegistrationState.EXPIRED, IsInside =
                context.ParkingMovements.Any(x => x.VehicleId == vehicle.Id && x.Status == ParkingMovementStatus.OPEN), VerificationImageId =
                context.VehicleVerificationImages.Where(x => x.VehicleId == vehicle.Id)
                    .OrderBy(x => x.CreatedAt).ThenBy(x => x.Id).Select(x => (Guid?)x.Id).FirstOrDefault() };
    }
    private sealed class VehicleRow
    {
        public Vehicle Vehicle { get; init; } = null!;
        public Guid? OwnerId { get; init; }
        public string? OwnerName { get; init; }
        public RegistrationState State { get; init; }
        public bool IsInside { get; init; }
        public Guid? VerificationImageId { get; init; }
    }
    private static VehicleView Map(VehicleRow row) => new(row.Vehicle.Id, row.Vehicle.Type, row.Vehicle.Plate?.Value,
        row.Vehicle.FrameNumber?.Value, row.Vehicle.Brand, row.Vehicle.Model, row.Vehicle.Color, row.Vehicle.Status,
        row.OwnerId, row.OwnerName, row.State, row.IsInside, row.VerificationImageId);
}
