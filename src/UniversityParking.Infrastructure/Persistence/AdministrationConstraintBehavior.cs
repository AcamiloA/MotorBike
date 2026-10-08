using MediatR;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using UniversityParking.Application.AcademicPeriods;
using UniversityParking.Application.ParkingLots;
using UniversityParking.Application.Common.Errors;
using UniversityParking.Application.Common.Results;

namespace UniversityParking.Infrastructure.Persistence;

public sealed class AdministrationConstraintBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull where TResponse : IResult<TResponse>
{
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        try { return await next(cancellationToken); }
        catch (DbUpdateException exception) when (request is CreateAcademicPeriodCommand or ActivateAcademicPeriodCommand or CreateParkingLotCommand or UpdateParkingLotCommand &&
            exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation } postgres &&
            postgres.ConstraintName is "ux_academic_periods_name" or "ux_academic_periods_single_active" or "ux_parking_lots_name_campus")
        {
            var constraint = ((PostgresException)exception.InnerException!).ConstraintName;
            return TResponse.Failure(constraint switch
            {
                "ux_academic_periods_name" => AcademicPeriodErrors.NameAlreadyExists,
                "ux_academic_periods_single_active" => AcademicPeriodErrors.AnotherActivePeriodExists,
                _ => ParkingErrors.LotAlreadyExists
            });
        }
    }
}
