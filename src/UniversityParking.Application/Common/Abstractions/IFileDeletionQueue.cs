namespace UniversityParking.Application.Common.Abstractions;
public interface IFileDeletionQueue
{
    Task EnqueueVehicleAsync(Guid vehicleId,CancellationToken token);
    Task<int> RetryAsync(CancellationToken token,Guid? vehicleId=null);
}
