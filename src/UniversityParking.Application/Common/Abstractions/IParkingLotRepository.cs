using UniversityParking.Domain.Parking;
using UniversityParking.Domain.Vehicles;
using UniversityParking.Application.ParkingLots;
using UniversityParking.Application.Common.Pagination;

namespace UniversityParking.Application.Common.Abstractions;

public interface IParkingLotRepository
{
    Task<ParkingLot?> GetByIdForUpdateAsync(Guid id, CancellationToken cancellationToken);
    Task<bool> ExistsByNameAndCampusAsync(string name, string campus, Guid? excludedId, CancellationToken cancellationToken);
    Task<PagedResult<ParkingLotView>> SearchAsync(ParkingLotStatus? status, PageRequest page, CancellationToken cancellationToken);
    Task AddZoneAsync(ParkingZone zone, CancellationToken cancellationToken);
    Task<ParkingLot?> GetByIdAsync(Guid id, CancellationToken cancellationToken);
    Task<IReadOnlyList<ParkingLot>> GetActiveAsync(CancellationToken cancellationToken);
    Task<ParkingZone?> GetZoneForVehicleTypeAsync(Guid parkingLotId, VehicleType type, CancellationToken cancellationToken);
    Task AddAsync(ParkingLot lot, CancellationToken cancellationToken);
}
