using UniversityParking.Domain.Vehicles;

namespace UniversityParking.Application.Common.Abstractions;

public interface IVehicleRegistrationRepository
{
    Task<VehicleRegistration?> GetByVehicleUserAndPeriodAsync(Guid vehicleId, Guid userId, Guid periodId, CancellationToken cancellationToken);
    Task<VehicleRegistration?> GetActiveForVehicleUserAndPeriodAsync(Guid vehicleId, Guid userId, Guid periodId, CancellationToken cancellationToken);
    Task<bool> ExistsForVehicleUserAndPeriodAsync(Guid vehicleId, Guid userId, Guid periodId, CancellationToken cancellationToken);
    Task AddAsync(VehicleRegistration registration, CancellationToken cancellationToken);
}
