using UniversityParking.Domain.Common;
namespace UniversityParking.Domain.Vehicles;
public sealed class PendingFileDeletion : Entity
{
    public Guid VehicleId {get;private set;}
    public string StorageKey {get;private set;} = null!;
    public DateTimeOffset CreatedAt {get;private set;}
    public DateTimeOffset? CompletedAt {get;private set;}
    public int Attempts {get;private set;}
    private PendingFileDeletion() {}
    public PendingFileDeletion(Guid vehicleId,string key,DateTimeOffset now) { VehicleId=Guard.Id(vehicleId,"vehículo");StorageKey=FileMetadata.StorageKey(key);CreatedAt=Guard.Utc(now); }
    public void Attempt()=>Attempts++;
    public void Complete(DateTimeOffset now)=>CompletedAt=Guard.Utc(now);
}
