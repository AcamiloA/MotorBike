using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using UniversityParking.Application.Common.Abstractions;
using UniversityParking.Infrastructure.Persistence;
using UniversityParking.Domain.Vehicles;
namespace UniversityParking.Infrastructure.Storage;
public sealed class FileDeletionQueue(AppDbContext db,IFileStorage storage,IClock clock,ILogger<FileDeletionQueue> logger):IFileDeletionQueue
{
    public async Task EnqueueVehicleAsync(Guid id,CancellationToken token)
    {
        var keys=await db.VehicleVerificationImages.Where(x=>x.VehicleId==id).Select(x=>x.StorageKey).ToListAsync(token);
        // Legacy photos/documents are historical evidence, not the active verification image.
        // Keep their metadata and binaries; archiving must not destroy historical documents.
        foreach(var key in keys.Distinct()) db.PendingFileDeletions.Add(new PendingFileDeletion(id,key,clock.UtcNow));
    }
    public async Task<int> RetryAsync(CancellationToken token,Guid? vehicleId=null)
    {
        var pending=await db.PendingFileDeletions.Where(x=>x.CompletedAt==null && (!vehicleId.HasValue||x.VehicleId==vehicleId.Value)).OrderBy(x=>x.CreatedAt).Take(100).ToListAsync(token);
        foreach(var item in pending)
        {
            item.Attempt();
            var reused=await db.VehicleVerificationImages.AnyAsync(x=>x.VehicleId!=item.VehicleId && x.StorageKey==item.StorageKey,token) ||
                await db.VehiclePhotos.AnyAsync(x=>x.StorageKey==item.StorageKey,token) ||
                await db.VehicleDocuments.AnyAsync(x=>x.StorageKey==item.StorageKey,token) ||
                await db.IncidentAttachments.AnyAsync(x=>x.StorageKey==item.StorageKey,token);
            if(reused) {logger.LogError("FILE_DELETION_SHARED_KEY: tarea {TaskId} requiere revisión.",item.Id);continue;}
            try { await storage.DeleteAsync(item.StorageKey,token);item.Complete(clock.UtcNow); }
            catch(OperationCanceledException) when(token.IsCancellationRequested) {throw;}
            catch {logger.LogError("FILE_DELETION_FAILED: tarea {TaskId}, intento {Attempt}.",item.Id,item.Attempts);}
        }
        await db.SaveChangesAsync(token);return pending.Count(x=>x.CompletedAt==null);
    }
}
