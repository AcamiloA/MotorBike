using MediatR;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using UniversityParking.Application.Common.Errors;
using UniversityParking.Application.Common.Results;
using UniversityParking.Application.Vehicles;

namespace UniversityParking.Infrastructure.Persistence;

public sealed class VehicleConstraintBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull where TResponse : IResult<TResponse>
{
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        try { return await next(cancellationToken); }
        catch (DbUpdateException exception) when (request is RegisterVehicleCommand or CorrectVehicleIdentifierCommand or TransferVehicleCommand or RenewVehicleRegistrationCommand &&
            exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation } postgres &&
            postgres.ConstraintName is "ux_vehicles_plate" or "ux_vehicles_frame_number" or "ux_vehicle_ownerships_current_vehicle" or "ux_vehicle_registrations_vehicle_user_period")
        {
            var constraint = ((PostgresException)exception.InnerException!).ConstraintName;
            var error = constraint switch
            {
                "ux_vehicle_ownerships_current_vehicle" => VehicleErrors.AlreadyHasCurrentOwner,
                "ux_vehicle_registrations_vehicle_user_period" => VehicleErrors.RegistrationAlreadyExists,
                _ => VehicleErrors.IdentifierAlreadyExists
            };
            return TResponse.Failure(error);
        }
    }
}
