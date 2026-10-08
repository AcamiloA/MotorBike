using Microsoft.EntityFrameworkCore;
using UniversityParking.Application.Common.Abstractions;
using UniversityParking.Application.Common.Pagination;
using UniversityParking.Application.ParkingLots;
using UniversityParking.Domain.Parking;
using UniversityParking.Domain.Vehicles;

namespace UniversityParking.Infrastructure.Persistence.Repositories;

public sealed class ParkingLotRepository(AppDbContext context) : IParkingLotRepository
{
    public Task<ParkingLot?> GetByIdAsync(Guid id, CancellationToken cancellationToken) => context.ParkingLots.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
    public async Task<ParkingLot?> GetByIdForUpdateAsync(Guid id, CancellationToken cancellationToken)
    {
        var lot = await context.ParkingLots.FromSqlInterpolated($"SELECT * FROM parking_lots WHERE id = {id} FOR UPDATE").SingleOrDefaultAsync(cancellationToken);
        if (lot is not null) await context.Entry(lot).ReloadAsync(cancellationToken);
        return lot;
    }
    public Task<bool> ExistsByNameAndCampusAsync(string name, string campus, Guid? excludedId, CancellationToken cancellationToken) =>
        context.ParkingLots.AnyAsync(x => x.Name == name && x.Campus == campus && (!excludedId.HasValue || x.Id != excludedId.Value), cancellationToken);
    public async Task<IReadOnlyList<ParkingLot>> GetActiveAsync(CancellationToken cancellationToken) => await context.ParkingLots.AsNoTracking().Where(x => x.Status == ParkingLotStatus.ACTIVE).OrderBy(x => x.Name).ThenBy(x => x.Id).ToListAsync(cancellationToken);
    public Task<ParkingZone?> GetZoneForVehicleTypeAsync(Guid parkingLotId, VehicleType type, CancellationToken cancellationToken) => context.ParkingZones.FirstOrDefaultAsync(x => x.ParkingLotId == parkingLotId && x.VehicleType == type, cancellationToken);
    public async Task AddAsync(ParkingLot lot, CancellationToken cancellationToken) => await context.ParkingLots.AddAsync(lot, cancellationToken);
    public async Task AddZoneAsync(ParkingZone zone, CancellationToken cancellationToken) => await context.ParkingZones.AddAsync(zone, cancellationToken);
    public async Task<PagedResult<ParkingLotView>> SearchAsync(ParkingLotStatus? status, PageRequest page, CancellationToken cancellationToken)
    {
        var query = context.ParkingLots.AsNoTracking();
        if (status.HasValue) query = query.Where(x => x.Status == status.Value);
        var total = await query.LongCountAsync(cancellationToken);
        if (page.Offset >= total) return new([], page.Page, page.PageSize, total);
        var lots = await query.OrderBy(x => x.Name).ThenBy(x => x.Id).Skip((int)page.Offset).Take(page.PageSize).ToListAsync(cancellationToken);
        var ids = lots.Select(x => x.Id).ToArray();
        var zones = (await context.ParkingZones.AsNoTracking().Where(x => ids.Contains(x.ParkingLotId)).ToListAsync(cancellationToken))
            .OrderBy(x => x.VehicleType).ToLookup(x => x.ParkingLotId);
        return new(lots.Select(x => new ParkingLotView(x.Id, x.Name, x.Campus, x.OpeningTime, x.ClosingTime, x.Status,
            zones[x.Id].Select(z => new ParkingZoneView(z.Id, z.Name, z.VehicleType, z.Status)).ToArray())), page.Page, page.PageSize, total);
    }
}
