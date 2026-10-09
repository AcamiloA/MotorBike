using Microsoft.EntityFrameworkCore;
using System.Net.Http.Json;
using UniversityParking.Api.E2E.Tests.Auth;
using UniversityParking.Contracts.Users;
using UniversityParking.Mobile.Core;
using UniversityParking.Domain.Users;

namespace UniversityParking.Api.E2E.Tests.Parking;

public sealed partial class ParkingEndpointTests
{
    [Fact]
    public async Task MobileAdministrativeUsersVehiclesNewsAndPeriodsUseRealPostgres()
    {
        var admin=await UserAsync(MemberType.STAFF,"ADMIN");var session=new AuthSession(new MobileStorage());var nav=new MobileNavigation();
        using var mobile=factory.CreateDefaultClient(new AuthHttpHandler(session,nav,new ApiOptions("https://localhost/")));mobile.BaseAddress=new("https://localhost/");
        var transport=new ApiClient(mobile);Assert.True((await new AuthService(transport,session,nav).LoginAsync(admin.IdentificationNumber.Value,AuthApiFixture.Password)).IsSuccess);var api=new AdminApiService(transport);
        var created=await api.CreateUserAsync(new("MOBILEADMINOWNER","Propietario nuevo",new Guid("a1100000-0000-4000-8000-000000000001"),null,UserMemberType.TEACHER,null,"Password1",UserType:UserInstitutionalType.TEACHER,Email:"teacher@example.com",PhoneNumber:"+573001234567",IdentificationType:"CC"));Assert.True(created.IsSuccess,created.Error?.Message);var ownerId=created.Value!.Id;
        Assert.Equal(ownerId,(await api.FindUserAsync("MOBILEADMINOWNER")).Value!.Id);
        Assert.True((await api.EditUserAsync(ownerId,new("Nombre editado",new Guid("a1100000-0000-4000-8000-000000000001"),"Opcional",UserMemberType.TEACHER,null,UserType:UserInstitutionalType.TEACHER,Email:"teacher@example.com",PhoneNumber:"+573001234567"))).IsSuccess);
        Assert.True((await api.AssignRoleAsync(ownerId,"ADMIN")).IsSuccess);Assert.True((await api.RemoveRoleAsync(ownerId,"ADMIN")).IsSuccess);
        Assert.False((await api.RemoveRoleAsync(ownerId,"USER")).IsSuccess);
        Assert.True((await api.UserStatusAsync(ownerId,false)).IsSuccess);Assert.Equal("INACTIVE",(await api.UserAsync(ownerId)).Value!.Status);Assert.True((await api.UserStatusAsync(ownerId,true)).IsSuccess);
        Assert.Equal(vehicle.Id,Assert.Single((await api.VehiclesAsync(null,"MOTORCYCLE","ACTIVE",target.IdentificationNumber.Value,"ACTIVE",1)).Value!.Items).Id);
        Assert.True((await api.CorrectAsync(vehicle.Id,"MOB123","Corrección desde Android")).IsSuccess);Assert.Equal("MOB123",(await api.FindVehicleAsync("MOB123")).Value!.Plate);
        Assert.True((await api.VehicleStatusAsync(vehicle.Id,false)).IsSuccess);Assert.True((await api.VehicleStatusAsync(vehicle.Id,true)).IsSuccess);
        Assert.True((await api.TransferAsync(vehicle.Id,"MOBILEADMINOWNER","Cambio de propietario")).IsSuccess);
        var transferred=(await api.VehicleAsync(vehicle.Id)).Value!.Vehicle;Assert.Equal(ownerId,transferred.CurrentOwnerId);Assert.NotEqual("ACTIVE",transferred.RegistrationState);
        var history=(await api.VehicleHistoryAsync(vehicle.Id)).Value!;Assert.Equal(2,history.Ownerships.TotalCount);Assert.Equal("CANCELLED",Assert.Single(history.Registrations.Items).Status);
        Assert.Equal(1,(await api.UserHistoryAsync(ownerId)).Value!.Ownerships.TotalCount);
        var news=await api.CreateNewsAsync("Noticia móvil","Contenido inicial");Assert.True(news.IsSuccess);var newsId=news.Value!.Id;
        Assert.Equal("DRAFT",Assert.Single((await api.NewsAsync("DRAFT","Noticia móvil",1)).Value!.Items).Status);
        Assert.True((await api.EditNewsAsync(newsId,"Noticia móvil editada","Contenido final")).IsSuccess);Assert.True((await api.NewsActionAsync(newsId,true)).IsSuccess);
        Assert.Equal("PUBLISHED",Assert.Single((await api.NewsAsync("PUBLISHED",null,1)).Value!.Items).Status);Assert.True((await api.NewsActionAsync(newsId,false)).IsSuccess);
        Assert.Equal("ARCHIVED",Assert.Single((await api.NewsAsync("ARCHIVED",null,1)).Value!.Items).Status);
        Assert.True((await api.PeriodActionAsync(period.Id,false)).IsSuccess);var next=await api.CreatePeriodAsync(new("2027-1",new(2027,1,1),new(2027,6,30)));Assert.True(next.IsSuccess);
        Assert.Equal("PLANNED",(await api.PeriodsAsync()).Value!.Single(x=>x.Id==next.Value!.Id).Status);
        Assert.True((await api.PeriodActionAsync(next.Value!.Id,true)).IsSuccess);Assert.True((await api.PeriodActionAsync(next.Value.Id,false)).IsSuccess);Assert.False((await api.PeriodActionAsync(next.Value.Id,true)).IsSuccess);
        await using var context=fixture.CreateContext();Assert.Equal(2,await context.VehicleOwnerships.CountAsync(x=>x.VehicleId==vehicle.Id));Assert.Equal(ownerId,(await context.VehicleOwnerships.SingleAsync(x=>x.VehicleId==vehicle.Id&&x.EndAt==null)).UserId);
    }
    [Fact]
    public async Task MobileAdministrativeLotsIncidentsReportsAuditAndGuardBoundaryUseRealApi()
    {
        var admin=await UserAsync(MemberType.STAFF,"ADMIN");await EnterAsync();clock.UtcNow=clock.UtcNow.AddMinutes(19);await client.PostAsJsonAsync("/api/v1/parking/check-out",new UniversityParking.Contracts.Parking.CheckOutVehicleRequest(await MovementIdAsync(), vehicle.Id));
        var session=new AuthSession(new MobileStorage());var nav=new MobileNavigation();using var mobile=factory.CreateDefaultClient(new AuthHttpHandler(session,nav,new ApiOptions("https://localhost/")));mobile.BaseAddress=new("https://localhost/");var transport=new ApiClient(mobile);
        Assert.True((await new AuthService(transport,session,nav).LoginAsync(admin.IdentificationNumber.Value,AuthApiFixture.Password)).IsSuccess);var api=new AdminApiService(transport);
        var newLot=await api.CreateLotAsync(new("Móvil","Sede",new(6,0),new(22,0)));Assert.True(newLot.IsSuccess);var lotId=newLot.Value!.Id;var createdLot=(await api.LotsAsync(null,1)).Value!.Items.Single(x=>x.Id==lotId);Assert.Equal(4,createdLot.Zones.Count);
        Assert.True((await api.EditLotAsync(lotId,new("Móvil editado","Sede",new(7,0),new(21,0)))).IsSuccess);Assert.True((await api.LotStatusAsync(lotId,false)).IsSuccess);Assert.True((await api.LotStatusAsync(lotId,true)).IsSuccess);
        var uploads=new GuardApiService(transport);var incident=await uploads.CreateIncidentAsync(new(new(lot.Id,target.Id,vehicle.Id),"OTHER","Observación administrativa",clock.UtcNow,[new("report.pdf","application/pdf","%PDF-1.7\n%%EOF"u8.ToArray())]));Assert.True(incident.IsSuccess,incident.Error?.Message);
        var detail=(await api.IncidentAsync(incident.Value!.Id)).Value!;Assert.NotEmpty((await api.FileAsync(Assert.Single(detail.Attachments).ContentUrl)).Value!);
        Assert.True((await api.ResolveAsync(incident.Value.Id,"Verificado y resuelto")).IsSuccess);Assert.Equal("RESOLVED",(await api.IncidentAsync(incident.Value.Id)).Value!.Incident.Status);
        var cancel=await uploads.CreateIncidentAsync(new(new(lot.Id),"OTHER","Duplicado",null,[]));Assert.True(cancel.IsSuccess);Assert.True((await api.CancelAsync(cancel.Value!.Id,"Duplicado confirmado")).IsSuccess);
        Assert.Equal("CANCELLED",(await api.IncidentAsync(cancel.Value.Id)).Value!.Incident.Status);
        var from=new DateOnly(2026,10,7);var to=from;
        Assert.True((await api.IncidentsAsync(lot.Id,target.Id,vehicle.Id,"OTHER","RESOLVED",from,to,1)).IsSuccess);
        Assert.Equal(1,Assert.Single((await api.DailyAsync(from,to,lot.Id)).Value!).CheckIns);
        Assert.Contains((await api.GroupAsync(true,from,to,lot.Id)).Value!,x=>x.Type=="MOTORCYCLE"&&x.CheckOuts==1);
        Assert.Contains((await api.GroupAsync(false,from,to,lot.Id)).Value!,x=>x.Type=="STUDENT"&&x.CheckIns==1);
        Assert.Equal(1,(await api.GuardActivityAsync(guard.Id,from,to)).Value!.CheckOuts);
        Assert.Equal(1,(await api.VehicleHistoryAsync(vehicle.Id)).Value!.Movements.TotalCount);Assert.Equal(1,(await api.UserHistoryAsync(target.Id)).Value!.Movements.TotalCount);
        Assert.Equal("CLOSED",Assert.Single((await api.HistoryAsync(lot.Id,target.IdentificationNumber.Value,vehicle.Plate!.Value,null,"MOTORCYCLE","CLOSED",from,to,1)).Value!.Items).Status);
        Assert.Equal(1,(await api.DashboardAsync()).Value!.TodayCheckOuts);
        var audit=await api.AuditAsync(admin.Id,"INCIDENT_RESOLVED","Incident",incident.Value.Id,from,to,1);Assert.Equal(incident.Value.Id,Assert.Single(audit.Value!.Items).EntityId);
        Assert.Equal(403,(await new GuardApiService(transport).CheckInAsync(target.Id,vehicle.Id,lot.Id)).Error!.HttpStatus);
    }
}
