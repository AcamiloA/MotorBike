using MediatR;
using UniversityParking.Application.Common.Messaging;
using UniversityParking.Application.Common.Results;
using UniversityParking.Application.Common.Errors;
using UniversityParking.Application.Common.Abstractions;
namespace UniversityParking.Application.Vehicles;
public sealed record ArchiveVehicleCommand(Guid VehicleId):ICommand;
public sealed record RetryVehicleFileDeletionsCommand:ICommand<int>;
public sealed class ArchiveVehicleHandler(VehicleOperationContext operation,IParkingMovementRepository movements,IUnitOfWork work,IFileDeletionQueue files):IRequestHandler<ArchiveVehicleCommand,Result>
{
    public async Task<Result> Handle(ArchiveVehicleCommand request,CancellationToken token)
    {
        await using(var transaction=await work.BeginTransactionAsync(token))
        {
            var access=await operation.AuthorizedVehicleAsync(request.VehicleId,token,ownerOnly:true,locked:true);
            if(access.IsFailure) return Result.Failure(access.Error!);
            if(await movements.ExistsOpenByVehicleIdAsync(request.VehicleId,token)) return Result.Failure(VehicleErrors.HasOpenParkingMovement);
            await files.EnqueueVehicleAsync(request.VehicleId,token);access.Value.Archive(operation.UtcNow);
            await operation.AuditAsync("VEHICLE_ARCHIVED",request.VehicleId,null,new{access.Value.DeletedAt},token);
            await work.SaveChangesAsync(token);await transaction.CommitAsync(token);
        }
        // The durable queue survives interruption and S3 failures after the DB commit.
        await files.RetryAsync(token,request.VehicleId);return Result.Success();
    }
}
public sealed class RetryVehicleFileDeletionsHandler(UniversityParking.Application.Users.UserOperationContext operation,IFileDeletionQueue files):IRequestHandler<RetryVehicleFileDeletionsCommand,Result<int>>
{
    public async Task<Result<int>> Handle(RetryVehicleFileDeletionsCommand request,CancellationToken token)
    { if(await operation.CheckAccessAsync(true,token) is {} error) return Result<int>.Failure(error);return Result<int>.Success(await files.RetryAsync(token)); }
}
