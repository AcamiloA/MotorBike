using UniversityParking.Domain.Vehicles;

namespace UniversityParking.Application.Common.Abstractions;

public interface IVehicleOwnershipRepository
{
    Task<VehicleOwnership?> GetCurrentByVehicleIdAsync(Guid vehicleId, CancellationToken cancellationToken);
    Task<VehicleOwnership?> GetCurrentByVehicleAndUserAsync(Guid vehicleId, Guid userId, CancellationToken cancellationToken);
    Task<IReadOnlyList<VehicleOwnership>> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken);
    Task AddAsync(VehicleOwnership ownership, CancellationToken cancellationToken);
}
