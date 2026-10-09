using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using UniversityParking.Application.Documents;
using UniversityParking.Application.Files;
using UniversityParking.Application.Common.Results;
using UniversityParking.Contracts.Vehicles;
using UniversityParking.Contracts.Parking;
using UniversityParking.Domain.Users;
using UniversityParking.Domain.Vehicles;
using UniversityParking.Domain.Parking;
namespace UniversityParking.Api.E2E.Tests.Vehicles;
public sealed partial class VehicleEndpointTests
{
    [Theory][InlineData(MemberType.STUDENT)][InlineData(MemberType.TEACHER)]
    public async Task ScooterRequiresOnlySerialAndPhotoAndNeverInvokesOcr(MemberType member)
    {
        var owner=await CreateUserAsync(member);var validation=new UnexpectedLicense();
        await using var otherFactory=CreateFactory(s=>{s.RemoveAll<ITransitLicenseValidationService>();s.AddSingleton<ITransitLicenseValidationService>(validation);});using var other=otherFactory.CreateClient(new(){BaseAddress=new("https://localhost")});await LoginAsync(owner,other);
        using var form=Form("SCOOTER"," serial123 ");var response=await other.PostAsync("/api/v1/vehicles",form);Assert.Equal(HttpStatusCode.Created,response.StatusCode);var id=(await response.Content.ReadFromJsonAsync<VehicleCreatedResponse>())!.Id;
        var detail=(await other.GetFromJsonAsync<VehicleDetailResponse>($"/api/v1/vehicles/{id}"))!;
        Assert.Equal("SCOOTER",detail.Vehicle.Type);Assert.Equal("SERIAL123",detail.Vehicle.FrameNumber);Assert.Equal("SCOOTER_PHOTO",detail.VerificationImage!.Type);Assert.Equal(0,validation.Calls);
    }
    [Theory][InlineData(true)][InlineData(false)]
    public async Task ScooterRejectsMissingSerialOrPhoto(bool missingSerial)
    {
        await LoginAsync(await CreateUserAsync());using var form=Form("SCOOTER",missingSerial?"":"SERIAL",photo:missingSerial);
        Assert.Equal(HttpStatusCode.BadRequest,(await client.PostAsync("/api/v1/vehicles",form)).StatusCode);await using var db=fixture.CreateContext();Assert.Empty(await db.Vehicles.ToArrayAsync());
    }
    [Fact] public async Task ArchiveIsOwnerOnlyRemovesActiveImageAndAllowsIdentifierReuse()
    {
        var owner=await CreateUserAsync();await LoginAsync(owner);var id=await RegisterAsync("SCOOTER","SERIAL-REUSE");
        await using var db=fixture.CreateContext();var key=(await db.VehicleVerificationImages.SingleAsync()).StorageKey;var path=Path.Combine(root,key.Replace('/',Path.DirectorySeparatorChar));Assert.True(File.Exists(path));
        await LoginAsync(await CreateUserAsync(MemberType.STAFF,"ADMIN"));Assert.Equal(HttpStatusCode.NotFound,(await client.DeleteAsync($"/api/v1/vehicles/{id}")).StatusCode);
        await LoginAsync(owner);Assert.Equal(HttpStatusCode.NoContent,(await client.DeleteAsync($"/api/v1/vehicles/{id}")).StatusCode);
        Assert.Empty((await client.GetFromJsonAsync<VehicleResponse[]>("/api/v1/vehicles/me"))!);Assert.False(File.Exists(path));db.ChangeTracker.Clear();Assert.NotNull((await db.Vehicles.SingleAsync()).DeletedAt);Assert.Single(await db.VehicleVerificationImages.ToArrayAsync());Assert.NotNull((await db.PendingFileDeletions.SingleAsync()).CompletedAt);
        Assert.Equal(HttpStatusCode.NotFound,(await client.PutAsJsonAsync($"/api/v1/vehicles/{id}",new UpdateVehicleRequest("brand","model","color"))).StatusCode);
        var next=await RegisterAsync("SCOOTER","SERIAL-REUSE");Assert.NotEqual(id,next);Assert.Equal(2,await db.Vehicles.CountAsync());
    }
    [Fact] public async Task ScooterSupportsGuardEntryExitHistoryAndArchiveBlocksOpenMovement()
    {
        var owner=await CreateUserAsync();await LoginAsync(owner);var id=await RegisterAsync("SCOOTER","SERIAL-ACCESS");var guard=await CreateUserAsync(MemberType.STAFF,"GUARD");
        await using var db=fixture.CreateContext();var now=DateTimeOffset.UtcNow;var lot=new ParkingLot("Test lot","Campus",new(0,0),new(23,59),now);db.AddRange(lot,new ParkingZone(lot.Id,"Scooters",VehicleType.SCOOTER,now));await db.SaveChangesAsync();
        await LoginAsync(guard);var lookup=await client.PostAsJsonAsync("/api/v1/parking/access/lookup",new ParkingAccessRequest(IdentificationNumber:owner.IdentificationNumber.Value));Assert.Equal("SCOOTER_PHOTO",(await lookup.Content.ReadFromJsonAsync<ParkingAccessResponse>())!.EligibleVehicles.Single().VerificationImage!.Type);
        var entry=await client.PostAsJsonAsync("/api/v1/parking/check-in",new CheckInVehicleRequest(owner.Id,id,lot.Id));Assert.Equal(HttpStatusCode.Created,entry.StatusCode);var movement=(await entry.Content.ReadFromJsonAsync<ParkingMovementResponse>())!;
        await LoginAsync(owner);Assert.Equal(HttpStatusCode.Conflict,(await client.DeleteAsync($"/api/v1/vehicles/{id}")).StatusCode);
        await LoginAsync(guard);Assert.Equal(HttpStatusCode.OK,(await client.PostAsJsonAsync("/api/v1/parking/check-out",new CheckOutVehicleRequest(movement.MovementId,id))).StatusCode);
        await LoginAsync(owner);Assert.Equal(HttpStatusCode.NoContent,(await client.DeleteAsync($"/api/v1/vehicles/{id}")).StatusCode);
        db.ChangeTracker.Clear();var history=await db.ParkingMovements.SingleAsync();Assert.Equal(ParkingMovementStatus.CLOSED,history.Status);Assert.Equal(id,history.VehicleId);Assert.Equal(1,await db.Vehicles.CountAsync());
        var response=await client.GetAsync("/api/v1/parking/history/me");Assert.Equal(HttpStatusCode.OK,response.StatusCode);Assert.Contains(movement.MovementId.ToString(),await response.Content.ReadAsStringAsync());
    }
    private sealed class UnexpectedLicense:ITransitLicenseValidationService
    {public int Calls;public Task<Result> ValidateAsync(ValidatedUpload upload,CancellationToken token){Calls++;throw new InvalidOperationException("Scooter must never invoke OCR.");}}
}
