using Microsoft.EntityFrameworkCore;
using UniversityParking.Application.Common.Abstractions;
using UniversityParking.Domain.Vehicles;
using UniversityParking.Domain.Vehicles.ValueObjects;

namespace UniversityParking.Infrastructure.Persistence.Repositories;

public sealed partial class VehicleRepository(AppDbContext context) : IVehicleRepository
{
    public async Task<Vehicle?> GetByIdForUpdateAsync(Guid id, CancellationToken cancellationToken)
    {
        var vehicle = await context.Vehicles.FromSqlInterpolated($"SELECT * FROM vehicles WHERE id = {id} FOR UPDATE").SingleOrDefaultAsync(cancellationToken);
        if (vehicle is not null) await context.Entry(vehicle).ReloadAsync(cancellationToken);
        return vehicle;
    }
    public Task<bool> HasActiveCarOwnedByUserAsync(Guid userId, CancellationToken cancellationToken) =>
        (from vehicle in context.Vehicles
         join ownership in context.VehicleOwnerships on vehicle.Id equals ownership.VehicleId
         where ownership.UserId == userId && ownership.EndAt == null &&
             vehicle.Type == VehicleType.CAR && vehicle.Status == VehicleStatus.ACTIVE
         select vehicle.Id).AnyAsync(cancellationToken);
    public Task<Vehicle?> GetByIdAsync(Guid id, CancellationToken cancellationToken) => context.Vehicles.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
    public Task<Vehicle?> GetByPlateAsync(VehiclePlate plate, CancellationToken cancellationToken) => context.Vehicles.FirstOrDefaultAsync(x => x.DeletedAt == null && x.Plate == plate, cancellationToken);
    public Task<Vehicle?> GetByFrameNumberAsync(FrameNumber frameNumber, CancellationToken cancellationToken) => context.Vehicles.FirstOrDefaultAsync(x => x.DeletedAt == null && x.FrameNumber == frameNumber, cancellationToken);
    public Task<bool> ExistsByPlateAsync(VehiclePlate plate, CancellationToken cancellationToken) => context.Vehicles.AnyAsync(x => x.DeletedAt == null && x.Plate == plate, cancellationToken);
    public Task<bool> ExistsByFrameNumberAsync(FrameNumber frameNumber, CancellationToken cancellationToken) => context.Vehicles.AnyAsync(x => x.DeletedAt == null && x.FrameNumber == frameNumber, cancellationToken);
    public async Task AddAsync(Vehicle vehicle, CancellationToken cancellationToken) => await context.Vehicles.AddAsync(vehicle, cancellationToken);
}
