using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using UniversityParking.Application.Common.Abstractions;
using UniversityParking.Domain.Vehicles;
using UniversityParking.Infrastructure.Persistence;
using UniversityParking.Infrastructure.Storage;
using UniversityParking.Infrastructure.Tests.Persistence;
namespace UniversityParking.Infrastructure.Tests.Storage;
[Collection("PostgreSQL persistence")]
public sealed class FileDeletionQueueTests(PostgresFixture fixture):IAsyncLifetime
{
    public Task InitializeAsync()=>fixture.ResetAsync();public Task DisposeAsync()=>Task.CompletedTask;
    [Fact] public async Task ExactActiveKeyIsDurableRetriedAndLegacyEvidenceRemains()
    {
        await using var db=fixture.CreateContext();var now=PersistenceTestData.Now;var vehicle=PersistenceTestData.Vehicle();
        var image=new VehicleVerificationImage(vehicle,"vehicles/active/image.png","image.png","image/png",8,now);
        db.AddRange(vehicle,image,new VehiclePhoto(vehicle.Id,VehiclePhotoType.GENERAL,"vehicles/legacy/photo.png","legacy.png","image/png",8,now));await db.SaveChangesAsync();
        var files=new Files{Fail=true};var queue=new FileDeletionQueue(db,files,new Clock(),NullLogger<FileDeletionQueue>.Instance);
        await using(var tx=await db.Database.BeginTransactionAsync()){await queue.EnqueueVehicleAsync(vehicle.Id,default);vehicle.Archive(now);await db.SaveChangesAsync();await tx.CommitAsync();}
        Assert.Equal(1,await queue.RetryAsync(default));Assert.Null((await db.PendingFileDeletions.SingleAsync()).CompletedAt);Assert.Equal(image.StorageKey,Assert.Single(files.Deleted));
        files.Fail=false;Assert.Equal(0,await queue.RetryAsync(default));Assert.NotNull((await db.PendingFileDeletions.SingleAsync()).CompletedAt);Assert.Equal(2,(await db.PendingFileDeletions.SingleAsync()).Attempts);
        Assert.Single(await db.VehiclePhotos.ToArrayAsync());Assert.Single(await db.VehicleVerificationImages.ToArrayAsync());Assert.DoesNotContain("vehicles/legacy/photo.png",files.Deleted);
    }
    [Fact] public async Task SharedHistoricalKeyIsNotDeleted()
    {
        await using var db=fixture.CreateContext();var now=PersistenceTestData.Now;var vehicle=PersistenceTestData.Vehicle();
        db.AddRange(vehicle,new VehicleVerificationImage(vehicle,"shared/image.png","image.png","image/png",8,now),new VehiclePhoto(vehicle.Id,VehiclePhotoType.GENERAL,"shared/image.png","legacy.png","image/png",8,now));await db.SaveChangesAsync();
        var files=new Files();var queue=new FileDeletionQueue(db,files,new Clock(),NullLogger<FileDeletionQueue>.Instance);await queue.EnqueueVehicleAsync(vehicle.Id,default);vehicle.Archive(now);await db.SaveChangesAsync();
        Assert.Equal(1,await queue.RetryAsync(default));Assert.Empty(files.Deleted);Assert.Null((await db.PendingFileDeletions.SingleAsync()).CompletedAt);
    }
    [Fact] public async Task OwnerRetryProcessesOnlyTheRequestedVehicle()
    {
        await using var db=fixture.CreateContext();var first=PersistenceTestData.Vehicle();var second=PersistenceTestData.Vehicle();
        db.AddRange(first,second,new VehicleVerificationImage(first,"first/image.png","image.png","image/png",8,PersistenceTestData.Now),new VehicleVerificationImage(second,"second/image.png","image.png","image/png",8,PersistenceTestData.Now));await db.SaveChangesAsync();
        var files=new Files();var queue=new FileDeletionQueue(db,files,new Clock(),NullLogger<FileDeletionQueue>.Instance);await queue.EnqueueVehicleAsync(first.Id,default);await queue.EnqueueVehicleAsync(second.Id,default);first.Archive(PersistenceTestData.Now);second.Archive(PersistenceTestData.Now);await db.SaveChangesAsync();
        Assert.Equal(0,await queue.RetryAsync(default,first.Id));Assert.Equal("first/image.png",Assert.Single(files.Deleted));Assert.Null((await db.PendingFileDeletions.SingleAsync(x=>x.VehicleId==second.Id)).CompletedAt);
    }
    private sealed class Clock:IClock{public DateTimeOffset UtcNow=>PersistenceTestData.Now;}
    private sealed class Files:IFileStorage
    {
        public bool Fail;public List<string> Deleted {get;}=[];
        public Task DeleteAsync(string key,CancellationToken token){Deleted.Add(key);return Fail?Task.FromException(new IOException("Synthetic failure")):Task.CompletedTask;}
        public Task<StoredFile> UploadAsync(FileUpload upload,CancellationToken token)=>throw new NotSupportedException();
        public Task<Stream> OpenReadAsync(string key,CancellationToken token)=>throw new NotSupportedException();
        public Task<Uri?> GetReadUrlAsync(string key,CancellationToken token)=>throw new NotSupportedException();
    }
}
