using Microsoft.EntityFrameworkCore;
using UniversityParking.Application.Common.Abstractions;
using UniversityParking.Domain.Vehicles;

namespace UniversityParking.Infrastructure.Persistence.Repositories;

public sealed class VehicleOwnershipRepository(AppDbContext context) : IVehicleOwnershipRepository
{
    public Task<VehicleOwnership?> GetCurrentByVehicleIdAsync(Guid vehicleId, CancellationToken cancellationToken) => context.VehicleOwnerships.FirstOrDefaultAsync(x => x.VehicleId == vehicleId && x.EndAt == null, cancellationToken);
    public Task<VehicleOwnership?> GetCurrentByVehicleAndUserAsync(Guid vehicleId, Guid userId, CancellationToken cancellationToken) => context.VehicleOwnerships.FirstOrDefaultAsync(x => x.VehicleId == vehicleId && x.UserId == userId && x.EndAt == null, cancellationToken);
    public async Task<IReadOnlyList<VehicleOwnership>> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken) => await context.VehicleOwnerships.Where(x => x.UserId == userId).OrderBy(x => x.StartAt).ThenBy(x => x.Id).ToListAsync(cancellationToken);
    public async Task AddAsync(VehicleOwnership ownership, CancellationToken cancellationToken) => await context.VehicleOwnerships.AddAsync(ownership, cancellationToken);
}
