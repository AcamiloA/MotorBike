using System.Net;
using System.Net.Http.Json;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using UniversityParking.Api.E2E.Tests.Auth;
using UniversityParking.Application.Common.Abstractions;
using UniversityParking.Contracts.Parking;
using UniversityParking.Domain.Parking;
using UniversityParking.Domain.Users;
using UniversityParking.Domain.Users.ValueObjects;
using UniversityParking.Domain.Vehicles;

namespace UniversityParking.Api.E2E.Tests.Parking;

public sealed partial class ParkingEndpointTests
{
    private string Qr => Convert.ToBase64String(Encoding.UTF8.GetBytes(target.IdentificationNumber.Value));
    [Theory]
    [InlineData("")] [InlineData(" ")] [InlineData("bad!")] [InlineData("/w==")]
    [InlineData("IA==")] [InlineData("eyJyb2xlIjoiR1VBUkQifQ==")]
    public async Task InstitutionalQrRejectsInvalidIdentityWithoutCrashing(string qr)
        => await ErrorAsync(await client.PostAsJsonAsync("/api/v1/parking/access/lookup",new ParkingAccessRequest(qr)),HttpStatusCode.BadRequest,"INVALID_QR_IDENTITY");
    [Fact] public async Task OversizedQrAndObsoleteCardCodeAreRejected()
    {
        await ErrorAsync(await client.PostAsJsonAsync("/api/v1/parking/access/lookup",new ParkingAccessRequest(new('A',257))),HttpStatusCode.BadRequest,"INVALID_QR_IDENTITY");
        await ErrorAsync(await client.PostAsJsonAsync("/api/v1/parking/access/lookup",new {cardCode=target.CardCode.Value}),HttpStatusCode.BadRequest,"VALIDATION_ERROR");
    }
    [Fact] public async Task QrAndManualUseSameIdentityEvenWhenAnotherCardMatchesRawPayload()
    {
        var other=await UserAsync(MemberType.STAFF);
        await using(var db=fixture.CreateContext())
        {
            var impostor=(await db.Users.FindAsync(other.Id))!;
            impostor.Update(impostor.FullName,impostor.UniversityId,impostor.Career,impostor.MemberType,new CardCode(Qr),clock.UtcNow);
            await db.SaveChangesAsync();
        }
        var qr=(await (await client.PostAsJsonAsync("/api/v1/parking/access/lookup",new ParkingAccessRequest(Qr))).Content.ReadFromJsonAsync<ParkingAccessResponse>())!;
        var manual=(await (await client.PostAsJsonAsync("/api/v1/parking/access/lookup",new ParkingAccessRequest(null,target.IdentificationNumber.Value))).Content.ReadFromJsonAsync<ParkingAccessResponse>())!;
        Assert.Equal(target.Id,qr.User.Id);Assert.Equal(manual.User,qr.User);Assert.Equal(manual.EligibleVehicles.Single(),qr.EligibleVehicles.Single());
        var movement=await EnterAsync();
        var after=(await (await client.PostAsJsonAsync("/api/v1/parking/access/lookup",new ParkingAccessRequest(Qr))).Content.ReadFromJsonAsync<ParkingAccessResponse>())!;
        Assert.Equal(movement.MovementId,after.CurrentMovement!.MovementId);Assert.Empty(after.EligibleVehicles);Assert.Equal(vehicle.Id,after.CurrentVehicle!.Id);
    }
    [Theory] [InlineData(VehicleType.CAR)] [InlineData(VehicleType.MOTORCYCLE)] [InlineData(VehicleType.BICYCLE)]
    public async Task LookupProjectsOnlyPrivateAuthoritativeEvidence(VehicleType type)
    {
        var owner=type==VehicleType.CAR?await UserAsync(MemberType.TEACHER):target;
        var selected=type==VehicleType.MOTORCYCLE?vehicle:await VehicleAsync(owner,type);
        var response=await client.PostAsJsonAsync("/api/v1/parking/access/lookup",new ParkingAccessRequest(null,owner.IdentificationNumber.Value));
        var body=await response.Content.ReadAsStringAsync();var result=(await response.Content.ReadFromJsonAsync<ParkingAccessResponse>())!;
        var image=result.EligibleVehicles.Single(x=>x.Id==selected.Id).VerificationImage!;
        Assert.Equal(type==VehicleType.BICYCLE?"BICYCLE_PHOTO":"TRANSIT_LICENSE_FRONT",image.Type);
        Assert.Equal($"/api/v1/vehicles/{selected.Id}/verification-image/content",image.ContentUrl);
        Assert.DoesNotContain("storageKey",body,StringComparison.OrdinalIgnoreCase);Assert.DoesNotContain("GENERAL",body);Assert.DoesNotContain("base64",body,StringComparison.OrdinalIgnoreCase);
    }
    [Fact] public async Task OpenVehicleExcludesOtherEligibleVehiclesAndSupportsMultipleDailyCycles()
    {
        var bicycle=await VehicleAsync(target,VehicleType.BICYCLE);
        var before=(await (await client.PostAsJsonAsync("/api/v1/parking/access/lookup",new ParkingAccessRequest(Qr))).Content.ReadFromJsonAsync<ParkingAccessResponse>())!;
        Assert.Equal(2,before.EligibleVehicles.Count);
        var first=await EnterAsync();
        var open=(await (await client.PostAsJsonAsync("/api/v1/parking/access/lookup",new ParkingAccessRequest(Qr))).Content.ReadFromJsonAsync<ParkingAccessResponse>())!;
        Assert.Empty(open.EligibleVehicles);Assert.Equal(vehicle.Id,open.CurrentVehicle!.Id);Assert.Equal(first.MovementId,open.CurrentMovement!.MovementId);
        clock.UtcNow=clock.UtcNow.AddMinutes(10);
        Assert.Equal(HttpStatusCode.OK,(await client.PostAsJsonAsync("/api/v1/parking/check-out",new CheckOutVehicleRequest(first.MovementId,vehicle.Id))).StatusCode);
        clock.UtcNow=clock.UtcNow.AddMinutes(10);var second=await EnterAsync(bicycle);
        Assert.Equal(bicycle.Id,second.VehicleId);Assert.NotEqual(first.MovementId,second.MovementId);
    }
    [Fact] public async Task StaleExactExitCannotCloseNewMovementAndLegacyBodyIsNotOperational()
    {
        var first=await EnterAsync();clock.UtcNow=clock.UtcNow.AddMinutes(5);
        Assert.Equal(HttpStatusCode.OK,(await client.PostAsJsonAsync("/api/v1/parking/check-out",new CheckOutVehicleRequest(first.MovementId,vehicle.Id))).StatusCode);
        clock.UtcNow=clock.UtcNow.AddMinutes(5);var second=await EnterAsync();
        await ErrorAsync(await client.PostAsJsonAsync("/api/v1/parking/check-out",new CheckOutVehicleRequest(first.MovementId,vehicle.Id)),HttpStatusCode.Conflict,"VEHICLE_NOT_INSIDE");
        await ErrorAsync(await client.PostAsJsonAsync("/api/v1/parking/check-out",new {vehicleId=vehicle.Id}),HttpStatusCode.BadRequest,"VALIDATION_ERROR");
        Assert.Equal("OPEN",(await client.GetFromJsonAsync<ParkingMovementResponse>($"/api/v1/parking/movements/{second.MovementId}"))!.Status);
        Assert.Equal("CLOSED",(await client.GetFromJsonAsync<ParkingMovementResponse>($"/api/v1/parking/movements/{first.MovementId}"))!.Status);
    }
    [Fact] public async Task ExactExitRejectsDifferentVehicle()
    {
        var second=await VehicleAsync(target,VehicleType.BICYCLE);var movement=await EnterAsync();
        await ErrorAsync(await client.PostAsJsonAsync("/api/v1/parking/check-out",new CheckOutVehicleRequest(movement.MovementId,second.Id)),HttpStatusCode.Conflict,"VEHICLE_NOT_INSIDE");
        Assert.Equal("OPEN",(await client.GetFromJsonAsync<ParkingMovementResponse>($"/api/v1/parking/movements/{movement.MovementId}"))!.Status);
    }
    [Fact] public async Task MissingEvidencePreventsNewEntryButNeverTrapsHistoricalOpen()
    {
        await using(var db=fixture.CreateContext()) {db.VehicleVerificationImages.Remove(await db.VehicleVerificationImages.SingleAsync());await db.SaveChangesAsync();}
        var response=(await (await client.PostAsJsonAsync("/api/v1/parking/access/lookup",new ParkingAccessRequest(Qr))).Content.ReadFromJsonAsync<ParkingAccessResponse>())!;
        Assert.Empty(response.EligibleVehicles);Assert.Equal("VEHICLE_VERIFICATION_REQUIRED",response.EntryBlockCode);
        await ErrorAsync(await client.PostAsJsonAsync("/api/v1/parking/check-in",Entry()),HttpStatusCode.Conflict,"VEHICLE_VERIFICATION_REQUIRED");
        var legacy=new ParkingMovement(target.Id,vehicle.Id,lot.Id,zone.Id,clock.UtcNow,guard.Id);
        await using(var db=fixture.CreateContext())
        {
            db.ParkingMovements.Add(legacy);(await db.Users.FindAsync(target.Id))!.Deactivate(clock.UtcNow);(await db.Vehicles.FindAsync(vehicle.Id))!.Deactivate(clock.UtcNow);await db.SaveChangesAsync();
        }
        response=(await (await client.PostAsJsonAsync("/api/v1/parking/access/lookup",new ParkingAccessRequest(Qr))).Content.ReadFromJsonAsync<ParkingAccessResponse>())!;
        Assert.Equal(legacy.Id,response.CurrentMovement!.MovementId);Assert.Null(response.CurrentVehicle!.VerificationImage);Assert.Empty(response.EligibleVehicles);
        Assert.Equal(HttpStatusCode.OK,(await client.PostAsJsonAsync("/api/v1/parking/check-out",new CheckOutVehicleRequest(legacy.Id,vehicle.Id))).StatusCode);
    }
    [Fact] public async Task EvidenceRemovedAfterLookupIsRevalidatedBeforeEntry()
    {
        Assert.Equal(HttpStatusCode.OK,(await client.PostAsJsonAsync("/api/v1/parking/access/lookup",new ParkingAccessRequest(Qr))).StatusCode);
        await using(var db=fixture.CreateContext()) {db.VehicleVerificationImages.Remove(await db.VehicleVerificationImages.SingleAsync());await db.SaveChangesAsync();}
        await ErrorAsync(await client.PostAsJsonAsync("/api/v1/parking/check-in",Entry()),HttpStatusCode.Conflict,"VEHICLE_VERIFICATION_REQUIRED");
        await using var verify=fixture.CreateContext();Assert.Empty(await verify.ParkingMovements.ToArrayAsync());Assert.Empty(await verify.AuditLogs.ToArrayAsync());
    }
    [Fact] public async Task ReusingClosedMovementIdentityDoesNotCreateAnotherEntry()
    {
        var id = Guid.NewGuid();
        var first = await client.PostAsJsonAsync("/api/v1/parking/check-in", Entry() with { MovementId = id });
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(id, (await first.Content.ReadFromJsonAsync<ParkingMovementResponse>())!.MovementId);
        clock.UtcNow = clock.UtcNow.AddMinutes(1);
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/api/v1/parking/check-out", new CheckOutVehicleRequest(id, vehicle.Id))).StatusCode);
        clock.UtcNow = clock.UtcNow.AddMinutes(1);
        await ErrorAsync(await client.PostAsJsonAsync("/api/v1/parking/check-in", Entry() with { MovementId = id }), HttpStatusCode.Conflict, "PARKING_MOVEMENT_ID_ALREADY_USED");
        await using var db = fixture.CreateContext();
        Assert.Equal(ParkingMovementStatus.CLOSED, (await db.ParkingMovements.SingleAsync()).Status);
    }
    [Fact] public async Task MovementReadAndVehicleMetadataRemainPrivateAndDoNotRunOcr()
    {
        var ocr=new TestDocumentTextExtractor();
        await using var guardedFactory=CreateFactory(s=>{s.RemoveAll<IDocumentTextExtractor>();s.AddSingleton<IDocumentTextExtractor>(ocr);});
        using var guardedClient=guardedFactory.CreateClient(new(){BaseAddress=new Uri("https://localhost")});await LoginAsync(guard,guardedClient);
        var id=Guid.NewGuid();var entered=await guardedClient.PostAsJsonAsync("/api/v1/parking/check-in",Entry() with{MovementId=id});
        Assert.Equal(id,(await entered.Content.ReadFromJsonAsync<ParkingMovementResponse>())!.MovementId);
        Assert.Equal(id,(await guardedClient.GetFromJsonAsync<ParkingMovementResponse>($"/api/v1/parking/movements/{id}"))!.MovementId);
        var detail=(await guardedClient.GetFromJsonAsync<EligibleVehicleResponse>($"/api/v1/parking/movements/{id}/vehicle"))!;
        Assert.Equal(vehicle.Id,detail.Id);Assert.NotNull(detail.VerificationImage);
        Assert.Equal(HttpStatusCode.OK,(await guardedClient.PostAsJsonAsync("/api/v1/parking/access/lookup",new ParkingAccessRequest(Qr))).StatusCode);
        Assert.Equal(HttpStatusCode.OK,(await guardedClient.PostAsJsonAsync("/api/v1/parking/check-out",new CheckOutVehicleRequest(id,vehicle.Id))).StatusCode);
        Assert.Equal(0,ocr.Calls);
        await LoginAsync(target);Assert.Equal(HttpStatusCode.Forbidden,(await client.GetAsync($"/api/v1/parking/movements/{id}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,(await client.GetAsync($"/api/v1/parking/movements/{id}/vehicle")).StatusCode);
    }
}
