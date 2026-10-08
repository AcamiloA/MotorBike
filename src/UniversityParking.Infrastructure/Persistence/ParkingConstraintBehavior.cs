using MediatR;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using UniversityParking.Application.Common.Errors;
using UniversityParking.Application.Common.Results;
using UniversityParking.Application.Parking;
using UniversityParking.Domain.Parking;

namespace UniversityParking.Infrastructure.Persistence;

public sealed class ParkingConstraintBehavior<TRequest, TResponse>(AppDbContext context) : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull where TResponse : IResult<TResponse>
{
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        try { return await next(cancellationToken); }
        catch (DbUpdateException exception) when (request is CheckInVehicleCommand &&
            exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation } postgres &&
            postgres.ConstraintName is "ux_parking_movements_open_vehicle" or "ux_parking_movements_open_user")
        {
            var command = (CheckInVehicleCommand)(object)request;
            var vehicleOpen = await context.ParkingMovements.AsNoTracking().AnyAsync(x => x.VehicleId == command.VehicleId && x.Status == ParkingMovementStatus.OPEN, cancellationToken);
            return TResponse.Failure(vehicleOpen ? ParkingErrors.VehicleAlreadyInside : ParkingErrors.UserAlreadyHasVehicleInside);
        }
    }
}
