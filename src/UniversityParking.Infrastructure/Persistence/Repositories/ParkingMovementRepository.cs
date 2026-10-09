using Microsoft.EntityFrameworkCore;
using UniversityParking.Application.Common.Abstractions;
using UniversityParking.Domain.Parking;

namespace UniversityParking.Infrastructure.Persistence.Repositories;

public sealed class ParkingMovementRepository(AppDbContext context) : IParkingMovementRepository
{
    public Task<bool> ExistsOpenByParkingLotIdAsync(Guid parkingLotId, CancellationToken cancellationToken) =>
        context.ParkingMovements.AnyAsync(x => x.ParkingLotId == parkingLotId && x.Status == ParkingMovementStatus.OPEN, cancellationToken);
    public Task<ParkingMovement?> GetByIdAsync(Guid id, CancellationToken cancellationToken) => context.ParkingMovements.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
    public Task<ParkingMovement?> GetByIdForUpdateAsync(Guid id, CancellationToken cancellationToken) =>
        context.ParkingMovements.FromSqlInterpolated($"SELECT * FROM parking_movements WHERE id = {id} FOR UPDATE").SingleOrDefaultAsync(cancellationToken);
    public Task<ParkingMovement?> GetOpenByVehicleIdAsync(Guid vehicleId, CancellationToken cancellationToken) => context.ParkingMovements.FirstOrDefaultAsync(x => x.VehicleId == vehicleId && x.Status == ParkingMovementStatus.OPEN, cancellationToken);
    public Task<ParkingMovement?> GetOpenByUserIdAsync(Guid userId, CancellationToken cancellationToken) => context.ParkingMovements.FirstOrDefaultAsync(x => x.UserId == userId && x.Status == ParkingMovementStatus.OPEN, cancellationToken);
    public Task<bool> ExistsOpenByVehicleIdAsync(Guid vehicleId, CancellationToken cancellationToken) => context.ParkingMovements.AnyAsync(x => x.VehicleId == vehicleId && x.Status == ParkingMovementStatus.OPEN, cancellationToken);
    public Task<bool> ExistsOpenByUserIdAsync(Guid userId, CancellationToken cancellationToken) => context.ParkingMovements.AnyAsync(x => x.UserId == userId && x.Status == ParkingMovementStatus.OPEN, cancellationToken);
    public async Task AddAsync(ParkingMovement movement, CancellationToken cancellationToken) => await context.ParkingMovements.AddAsync(movement, cancellationToken);
}
