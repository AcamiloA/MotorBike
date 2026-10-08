using Microsoft.EntityFrameworkCore;
using UniversityParking.Application.Common.Abstractions;
using UniversityParking.Domain.Vehicles;

namespace UniversityParking.Infrastructure.Persistence.Repositories;

public sealed class VehicleRegistrationRepository(AppDbContext context) : IVehicleRegistrationRepository
{
    public Task<VehicleRegistration?> GetByVehicleUserAndPeriodAsync(Guid vehicleId, Guid userId, Guid periodId, CancellationToken cancellationToken) => context.VehicleRegistrations.FirstOrDefaultAsync(x => x.VehicleId == vehicleId && x.UserId == userId && x.AcademicPeriodId == periodId, cancellationToken);
    public Task<VehicleRegistration?> GetActiveForVehicleUserAndPeriodAsync(Guid vehicleId, Guid userId, Guid periodId, CancellationToken cancellationToken) => context.VehicleRegistrations.FirstOrDefaultAsync(x => x.VehicleId == vehicleId && x.UserId == userId && x.AcademicPeriodId == periodId && x.Status == VehicleRegistrationStatus.ACTIVE, cancellationToken);
    public Task<bool> ExistsForVehicleUserAndPeriodAsync(Guid vehicleId, Guid userId, Guid periodId, CancellationToken cancellationToken) => context.VehicleRegistrations.AnyAsync(x => x.VehicleId == vehicleId && x.UserId == userId && x.AcademicPeriodId == periodId, cancellationToken);
    public async Task AddAsync(VehicleRegistration registration, CancellationToken cancellationToken) => await context.VehicleRegistrations.AddAsync(registration, cancellationToken);
}
