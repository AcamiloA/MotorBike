using System.Net;
using System.Net.Http.Json;
using UniversityParking.Contracts.Common;
using UniversityParking.Contracts.Parking;
using UniversityParking.Contracts.ParkingLots;
using UniversityParking.Contracts.Incidents;
using UniversityParking.Contracts.Users;
using UniversityParking.Mobile.Core;
using UniversityParking.Mobile.ViewModels;

namespace UniversityParking.Mobile.Tests;

public sealed partial class GuardFeatureTests
{
    private static readonly Guid LotId = Guid.NewGuid(), UserId = Guid.NewGuid(), VehicleId = Guid.NewGuid();
    private static ParkingLotResponse Lot(Guid? id = null, string status = "ACTIVE") => new(id ?? LotId,"Principal","Kennedy",new(6,0),new(22,0),status,[]);
    private static ParkingMovementResponse Movement(string status = "OPEN", Guid? id = null) => new(id ?? Guid.NewGuid(),UserId,"Usuario",VehicleId,"BICYCLE","FRAME123",LotId,"Principal",Guid.NewGuid(),"Bicicletas",DateTimeOffset.UtcNow.AddHours(-1),Guid.NewGuid(),status == "CLOSED" ? DateTimeOffset.UtcNow : null,null,status,TimeSpan.FromHours(1));
    private static ParkingAccessResponse Access(ParkingMovementResponse? movement = null, string status = "ACTIVE") => new(new(UserId,"Usuario","STUDENT",status),movement,[new(VehicleId,"BICYCLE","FRAME123","Brand","Model","Black")]);
    private static HttpResponseMessage Ok(object value) => new(HttpStatusCode.OK) { Content = JsonContent.Create(value) };
    private static HttpResponseMessage LotPage(params ParkingLotResponse[] lots) => Ok(new PagedResponse<ParkingLotResponse>(lots,1,100,lots.Length,lots.Length > 0 ? 1 : 0));
    private static async Task<(GuardApiService Api, GuardLotSession Lots, Transport Transport, AuthSession Session)> Setup(Func<HttpRequestMessage,Task<HttpResponseMessage>> send, params string[] roles)
    {
        var session = new AuthSession(new Storage()); await session.SaveAsync("token",new(Guid.NewGuid(),"GUARD123","Guarda",new Guid("a1100000-0000-4000-8000-000000000001"), "ETITC",null,"STAFF","CARD","ACTIVE",roles.Length == 0 ? ["GUARD"] : roles));
        var transport = new Transport(send); var api = new GuardApiService(new ApiClient(new HttpClient(transport) { BaseAddress = new("https://test.example/") }));
        return (api,new(api,session,new Store()),transport,session);
    }
    [Fact] public async Task OneActiveLotAutoSelectsAndInactiveLotsAreExcluded()
    { var setup=await Setup(_=>Task.FromResult(LotPage(Lot(),Lot(Guid.NewGuid(),"INACTIVE")))); await setup.Lots.RefreshAsync(); Assert.Equal(LotId,setup.Lots.RequireLot()); Assert.Single(setup.Lots.Lots); }
    [Fact] public async Task MultipleLotsRequireSelectionAndRememberItForSameUser()
    { var second=Lot(Guid.NewGuid()); var setup=await Setup(_=>Task.FromResult(LotPage(Lot(),second))); await setup.Lots.RefreshAsync(); Assert.Null(setup.Lots.Selected); Assert.Throws<UserInputException>(()=>setup.Lots.RequireLot()); setup.Lots.Selected=setup.Lots.Lots[1]; var id=setup.Lots.RequireLot(); await setup.Lots.RefreshAsync(); Assert.Equal(id,setup.Lots.RequireLot()); }
    [Fact] public async Task InactiveSelectionIsClearedOnRefresh()
    { var calls=0; var setup=await Setup(_=>Task.FromResult(++calls==1?LotPage(Lot()):LotPage())); await setup.Lots.RefreshAsync(); await setup.Lots.RefreshAsync(); Assert.Null(setup.Lots.Selected); Assert.Empty(setup.Lots.Lots); }
    [Fact] public async Task LotSelectionDoesNotLeakToAnotherSession()
    { var setup=await Setup(_=>Task.FromResult(LotPage(Lot(),Lot(Guid.NewGuid())))); await setup.Lots.RefreshAsync(); setup.Lots.Selected=setup.Lots.Lots[0]; await setup.Session.SaveAsync("other",setup.Session.User! with { Id=Guid.NewGuid() }); await setup.Lots.RefreshAsync(); Assert.Null(setup.Lots.Selected); }
    [Fact] public async Task InsideUsesSelectedLotAndEscapesSearchFilter()
    { string? query=null; var setup=await Setup(r=>{if(r.RequestUri!.AbsolutePath.EndsWith("parking-lots"))return Task.FromResult(LotPage(Lot()));query=r.RequestUri.Query;return Task.FromResult(Ok(new VehiclesInsideResponse([],1,20,0,0,new(0,0,0,0))));}); var vm=new VehiclesInsideViewModel(setup.Api,setup.Lots,new Navigation()) { Search="x&parkingLotId=other",SelectedType=GuardPresentation.VehicleTypes[3] }; await vm.LoadAsync(); Assert.True(vm.IsEmpty); Assert.Contains(LotId.ToString(),query); Assert.Contains("vehicleType=BICYCLE",query); Assert.Contains("%26",query); }
    [Fact] public async Task HistoryRejectsReversedDatesLocally()
    { var setup=await Setup(_=>throw new Exception()); var vm=new ParkingHistoryViewModel(setup.Api,setup.Lots) { DateFrom=new(2026,10,8),DateTo=new(2026,10,7) };await vm.LoadAsync();Assert.Contains("fecha final",vm.ErrorMessage);Assert.Equal(0,setup.Transport.Calls); }
    [Fact] public async Task IncidentMultipartKeepsMovementContextAndIndexedPrivateAttachments()
    { string? body=null; var setup=await Setup(async r=>{body=await r.Content!.ReadAsStringAsync(); return Ok(new IncidentCreatedResponse(Guid.NewGuid()));});var incident=new IncidentInput(new(LotId,UserId,VehicleId,Guid.NewGuid()),"DAMAGE","Daño real",GuardPresentation.BogotaInstant(new(2026,10,7),new(8,30,0)),[new("support.pdf","application/pdf","%PDF-1.7"u8.ToArray())]); Assert.True((await setup.Api.CreateIncidentAsync(incident)).IsSuccess); Assert.Contains("Attachments[0]",body);Assert.Contains("ParkingMovementId",body);Assert.Contains("2026-10-07T13:30:00",body); }
    [Fact] public async Task IncidentValidationAndUncertainResultPreventDuplicateCreation()
    { var posts=0;var setup=await Setup(r=>r.RequestUri!.AbsolutePath.EndsWith("parking-lots")?Task.FromResult(LotPage(Lot())):Fail());Task<HttpResponseMessage> Fail(){posts++;throw new TaskCanceledException();}await setup.Lots.RefreshAsync();var vm=new CreateIncidentViewModel(setup.Api,setup.Lots,new Picker(),new Navigation());await vm.SaveCommand.ExecuteAsync(null);Assert.Equal(0,posts);vm.Description="Descripción";await vm.SaveCommand.ExecuteAsync(null);Assert.True(vm.Completed);await vm.SaveCommand.ExecuteAsync(null);Assert.Equal(1,posts); }
    [Theory] [InlineData("VEHICLE_ALREADY_INSIDE","ya está dentro")] [InlineData("PARKING_LOT_CLOSED","cerrado")] [InlineData("USER_INACTIVE","inactivo")]
    public void AccessErrorsHaveSpanishMessages(string code,string expected) => Assert.Contains(expected,GuardPresentation.AccessError(new(code,"server")));
    [Fact] public async Task LotSelectorReadsAllPages()
    { var second=Lot(Guid.NewGuid());var setup=await Setup(r=>Task.FromResult(Ok(new PagedResponse<ParkingLotResponse>(r.RequestUri!.Query.Contains("page=2")?[second]:[Lot()],r.RequestUri.Query.Contains("page=2")?2:1,100,101,2))));await setup.Lots.RefreshAsync();Assert.Equal(2,setup.Lots.Lots.Count);Assert.Equal(2,setup.Transport.Calls);Assert.Null(setup.Lots.Selected); }
[Fact] public async Task IncidentFromMovementPreservesReferencesWithoutReentry()
    { string? body=null;var movement=Movement();var setup=await Setup(async r=>{if(r.RequestUri!.AbsolutePath.EndsWith("parking-lots"))return LotPage(Lot());body=await r.Content!.ReadAsStringAsync();return Ok(new IncidentCreatedResponse(Guid.NewGuid()));});await setup.Lots.RefreshAsync();var vm=new CreateIncidentViewModel(setup.Api,setup.Lots,new Picker(),new Navigation()) { Context=new(LotId,UserId,VehicleId,movement.MovementId,"Usuario","FRAME123","Principal"),Description="Daño" };await vm.SaveCommand.ExecuteAsync(null);Assert.True(vm.Completed);Assert.Contains(movement.MovementId.ToString(),body);Assert.Contains(UserId.ToString(),body);Assert.Contains(VehicleId.ToString(),body); }
private sealed class PendingPermission(Task<bool> task):IScannerPermission { public Task<bool> RequestAsync()=>task; }
    private sealed class Transport(Func<HttpRequestMessage,Task<HttpResponseMessage>> send):HttpMessageHandler { public int Calls; protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken token){Calls++;return send(request);} }
    private sealed class Storage:ISecretStorage { private string? token;public Task<string?> GetAsync(string key)=>Task.FromResult(token);public Task SetAsync(string key,string value){token=value;return Task.CompletedTask;}public void Remove(string key)=>token=null; }
    private sealed class Store:IGuardSelectionStore { private readonly Dictionary<Guid,Guid?> values=[];public Guid? Get(Guid user)=>values.GetValueOrDefault(user);public void Set(Guid user,Guid? value)=>values[user]=value; }
    private sealed class Permission(bool allowed):IScannerPermission { public Task<bool> RequestAsync()=>Task.FromResult(allowed); }
    private sealed class Navigation:IUserNavigation { public string? Route;public bool Confirm=true;public int ConfirmCalls;public Task GoAsync(string route,IReadOnlyDictionary<string,object>? arguments=null){Route=route;return Task.CompletedTask;}public Task BackAsync()=>Task.CompletedTask;public Task MessageAsync(string title,string message)=>Task.CompletedTask;public Task<bool> ConfirmAsync(string title,string message){ConfirmCalls++;return Task.FromResult(Confirm);} }
    private sealed class Picker:IAttachmentPicker {public Task<PickedAttachment?> DocumentAsync()=>Task.FromResult<PickedAttachment?>(null);public Task<PickedAttachment?> TransitLicenseAsync() => PhotoAsync(true); public Task<PickedAttachment?> PhotoAsync(bool camera)=>Task.FromResult<PickedAttachment?>(null);}
}
