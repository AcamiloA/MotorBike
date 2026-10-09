using UniversityParking.Domain.Parking;

namespace UniversityParking.Application.Common.Abstractions;

public interface IParkingMovementRepository
{
    Task<bool> ExistsOpenByParkingLotIdAsync(Guid parkingLotId, CancellationToken cancellationToken);
    Task<ParkingMovement?> GetByIdAsync(Guid id, CancellationToken cancellationToken);
    Task<ParkingMovement?> GetByIdForUpdateAsync(Guid id, CancellationToken cancellationToken);
    Task<ParkingMovement?> GetOpenByVehicleIdAsync(Guid vehicleId, CancellationToken cancellationToken);
    Task<ParkingMovement?> GetOpenByUserIdAsync(Guid userId, CancellationToken cancellationToken);
    Task<bool> ExistsOpenByVehicleIdAsync(Guid vehicleId, CancellationToken cancellationToken);
    Task<bool> ExistsOpenByUserIdAsync(Guid userId, CancellationToken cancellationToken);
    Task AddAsync(ParkingMovement movement, CancellationToken cancellationToken);
}
