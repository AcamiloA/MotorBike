using UniversityParking.Domain.Vehicles;
using UniversityParking.Domain.Vehicles.ValueObjects;
using UniversityParking.Application.Vehicles;
using UniversityParking.Application.Common.Pagination;

namespace UniversityParking.Application.Common.Abstractions;

public interface IVehicleRepository
{
    Task<Vehicle?> GetByIdForUpdateAsync(Guid id, CancellationToken cancellationToken);
    Task<VehicleView?> GetViewAsync(Guid id, CancellationToken cancellationToken);
    Task<IReadOnlyList<VehicleView>> GetByCurrentOwnerAsync(Guid userId, CancellationToken cancellationToken);
    Task<PagedResult<VehicleView>> SearchAsync(GetVehiclesQuery query, CancellationToken cancellationToken);
    Task<bool> HasActiveCarOwnedByUserAsync(Guid userId, CancellationToken cancellationToken);
    Task<Vehicle?> GetByIdAsync(Guid id, CancellationToken cancellationToken);
    Task<Vehicle?> GetByPlateAsync(VehiclePlate plate, CancellationToken cancellationToken);
    Task<Vehicle?> GetByFrameNumberAsync(FrameNumber frameNumber, CancellationToken cancellationToken);
    Task<bool> ExistsByPlateAsync(VehiclePlate plate, CancellationToken cancellationToken);
    Task<bool> ExistsByFrameNumberAsync(FrameNumber frameNumber, CancellationToken cancellationToken);
    Task AddAsync(Vehicle vehicle, CancellationToken cancellationToken);
}
