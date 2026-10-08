using UniversityParking.Application.Parking;
using UniversityParking.Application.Common.Pagination;

namespace UniversityParking.Application.Common.Abstractions;

public interface IParkingMovementReadRepository
{
    Task<ParkingMovementView?> GetByIdAsync(Guid id, DateTimeOffset now, CancellationToken cancellationToken);
    Task<PagedResult<ParkingMovementView>> SearchAsync(ParkingMovementFilter filter, DateTimeOffset now, CancellationToken cancellationToken);
    Task<VehiclesInsideView> GetInsideAsync(ParkingMovementFilter filter, DateTimeOffset now, CancellationToken cancellationToken);
}
