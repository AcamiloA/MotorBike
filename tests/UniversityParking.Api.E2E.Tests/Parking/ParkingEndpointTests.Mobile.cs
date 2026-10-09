using Microsoft.EntityFrameworkCore;
using UniversityParking.Api.E2E.Tests.Auth;
using UniversityParking.Contracts.Parking;
using UniversityParking.Contracts.Incidents;
using UniversityParking.Mobile.Core;

namespace UniversityParking.Api.E2E.Tests.Parking;

public sealed partial class ParkingEndpointTests
{
    [Theory]
    [InlineData(true)] [InlineData(false)]
    public async Task MobileGuardLoginLookupEntryIncidentInsideExitFlowUsesRealPostgres(bool scan)
    {
        var session = new AuthSession(new MobileStorage()); var nav = new MobileNavigation();
        using var mobileClient = factory.CreateDefaultClient(new AuthHttpHandler(session, nav, new ApiOptions("https://localhost/")));
        mobileClient.BaseAddress = new("https://localhost/"); var transport = new ApiClient(mobileClient);
        Assert.True((await new AuthService(transport,session,nav).LoginAsync(guard.IdentificationNumber.Value,AuthApiFixture.Password)).IsSuccess);
        var api = new GuardApiService(transport);
        Assert.Equal(lot.Id,Assert.Single((await api.LotsAsync()).Value!.Items).Id);
        var request = scan ? new ParkingAccessRequest(Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(target.IdentificationNumber.Value))) : new ParkingAccessRequest(null,target.IdentificationNumber.Value);
        var access = await api.LookupAsync(request); Assert.True(access.IsSuccess); Assert.Null(access.Value!.CurrentMovement); Assert.Equal(vehicle.Id,Assert.Single(access.Value.EligibleVehicles).Id);
        var entry = await api.CheckInAsync(target.Id,vehicle.Id,lot.Id); Assert.True(entry.IsSuccess,entry.Error?.Message); var movement = entry.Value!;
        Assert.Equal(vehicle.Id,(await api.LookupAsync(request)).Value!.CurrentMovement!.VehicleId);
        var inside = (await api.InsideAsync(lot.Id,"MOTORCYCLE",vehicle.Plate!.Value,1)).Value!; Assert.Equal(1,inside.Counts.Total); Assert.Equal(movement.MovementId,Assert.Single(inside.Items).MovementId);
        var incident = await api.CreateIncidentAsync(new(new(lot.Id,target.Id,vehicle.Id,movement.MovementId),"DAMAGE","Daño reportado desde Android",null,[new("support.pdf","application/pdf","%PDF-1.7\n%%EOF"u8.ToArray())]));
        Assert.True(incident.IsSuccess,incident.Error?.Message);
        var detail = (await api.IncidentAsync(incident.Value!.Id)).Value!; Assert.Equal(movement.MovementId,detail.Incident.ParkingMovementId); var attachment=Assert.Single(detail.Attachments);
        var file = await api.FileAsync(attachment.ContentUrl); Assert.True(file.IsSuccess); Assert.NotEmpty(file.Value!);
        Assert.Equal(1,(await api.DashboardAsync(lot.Id)).Value!.OpenIncidents);
        clock.UtcNow = clock.UtcNow.AddMinutes(47); var exit = await api.CheckOutAsync(await MovementIdAsync(), vehicle.Id); Assert.True(exit.IsSuccess); Assert.Equal(TimeSpan.FromMinutes(47),exit.Value!.Duration);
        Assert.Equal("CLOSED",exit.Value.Status); Assert.Empty((await api.InsideAsync(lot.Id,null,null,1)).Value!.Items);
        Assert.Null((await api.LookupAsync(request)).Value!.CurrentMovement);
        var history = await api.HistoryAsync(lot.Id,new(2026,10,7),new(2026,10,7),target.IdentificationNumber.Value,null,null,"MOTORCYCLE","CLOSED",1);
        Assert.Equal(movement.MovementId,Assert.Single(history.Value!.Items).MovementId);
        var dashboard=(await api.DashboardAsync(lot.Id)).Value!;Assert.Equal(0,dashboard.VehiclesInside);Assert.Equal(1,dashboard.TodayCheckIns);Assert.Equal(1,dashboard.TodayCheckOuts);
        await using var context=fixture.CreateContext();Assert.Single(await context.ParkingMovements.ToArrayAsync());Assert.Single(await context.Incidents.ToArrayAsync());
    }
    [Fact]
    public async Task MobileAdminWithoutGuardCannotCheckInOrCheckOut()
    {
        var admin=await UserAsync(UniversityParking.Domain.Users.MemberType.STAFF,"ADMIN");await LoginAsync(admin);
        var api=new GuardApiService(new ApiClient(client));
        Assert.True((await api.LookupAsync(new(Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(target.IdentificationNumber.Value))))).IsSuccess);
        Assert.Equal(403,(await api.CheckInAsync(target.Id,vehicle.Id,lot.Id)).Error!.HttpStatus);
        Assert.Equal(403,(await api.CheckOutAsync(await MovementIdAsync(), vehicle.Id)).Error!.HttpStatus);
        Assert.Equal(403,(await api.DashboardAsync(lot.Id)).Error!.HttpStatus);
    }
    private sealed class MobileStorage:ISecretStorage
    {private string? value;public Task<string?> GetAsync(string key)=>Task.FromResult(value);public Task SetAsync(string key,string token){value=token;return Task.CompletedTask;}public void Remove(string key)=>value=null;}
    private sealed class MobileNavigation:IAppNavigation
    {public Task ShowLoginAsync(string? message=null)=>Task.CompletedTask;public Task ShowAuthenticatedAsync()=>Task.CompletedTask;}
}
