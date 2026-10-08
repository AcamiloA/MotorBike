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

public sealed class GuardFeatureTests
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
    [Theory] [InlineData("USER")] [InlineData("ADMIN")]
    public async Task WithoutGuardCannotExecuteGuardCommands(string role)
    { var setup=await Setup(_=>throw new Exception("Unexpected HTTP"),role); var vm=new GuardLookupViewModel(setup.Api,setup.Lots,new Navigation(),new Permission(true)) { IdentificationNumber="123" }; await vm.SearchCommand.ExecuteAsync(null); Assert.Contains("GUARD",vm.ErrorMessage); Assert.Equal(0,setup.Transport.Calls); }
    [Fact] public async Task ScannerClaimsOneOpaqueCodeAndNeverExecutesUrl()
    {
        var pending=new TaskCompletionSource<HttpResponseMessage>(); string? json=null;
        var setup=await Setup(async request=>{json=await request.Content!.ReadAsStringAsync(); return await pending.Task;}); var nav=new Navigation(); var vm=new GuardLookupViewModel(setup.Api,setup.Lots,nav,new Permission(true));
        await vm.StartCommand.ExecuteAsync(null); var first=vm.ScanAsync("https://example.com/opaque"); await vm.ScanAsync("https://example.com/opaque"); Assert.Equal(1,setup.Transport.Calls); Assert.False(vm.IsScanning);
        pending.SetResult(Ok(Access())); await first; Assert.Contains("https://example.com/opaque",json); Assert.Equal("guard-access",nav.Route); await vm.ScanAsync("other"); Assert.Equal(1,setup.Transport.Calls);
    }
    [Fact] public async Task LeavingScannerSuppressesLateLookupNavigation()
    { var pending=new TaskCompletionSource<HttpResponseMessage>(); var setup=await Setup(_=>pending.Task); var nav=new Navigation(); var vm=new GuardLookupViewModel(setup.Api,setup.Lots,nav,new Permission(true)); await vm.StartCommand.ExecuteAsync(null); var scan=vm.ScanAsync("CARD"); vm.Stop(); pending.SetResult(Ok(Access())); await scan; Assert.Null(nav.Route); }
    [Fact] public async Task DeniedCameraStillAllowsManualLookup()
    { var setup=await Setup(_=>Task.FromResult(Ok(Access()))); var nav=new Navigation(); var vm=new GuardLookupViewModel(setup.Api,setup.Lots,nav,new Permission(false)); await vm.StartCommand.ExecuteAsync(null); Assert.False(vm.IsScanning); Assert.Contains("No se concedió",vm.ErrorMessage); vm.IdentificationNumber="123"; await vm.SearchCommand.ExecuteAsync(null); Assert.Equal("guard-access",nav.Route); }
    [Fact] public async Task InvalidIdentificationDoesNotCallBackend()
    { var setup=await Setup(_=>throw new Exception()); var vm=new GuardLookupViewModel(setup.Api,setup.Lots,new Navigation(),new Permission(true)); await vm.SearchCommand.ExecuteAsync(null); Assert.Contains("obligatorio",vm.ErrorMessage); Assert.Equal(0,setup.Transport.Calls); }
    [Theory] [InlineData("INACTIVE",false)] [InlineData("ACTIVE",true)]
    public async Task AccessResultOnlyAllowsEligibleActiveUserEntry(string status,bool allowed)
    { var setup=await Setup(_=>throw new Exception()); var vm=new AccessResultViewModel(setup.Lots,new Navigation()) { Access=new(new("CARD"),Access(status:status)) }; Assert.Equal(allowed,vm.CanEnter); Assert.Equal(allowed,vm.ShowVehicles); Assert.False(vm.CanExit); vm.Access=new(new("CARD"),Access(Movement(),status)); Assert.False(vm.CanEnter); Assert.False(vm.ShowVehicles); Assert.True(vm.CanExit); }
    [Fact] public async Task EntryCancellationAndDoubleTapNeverDuplicateMutation()
    {
        var posts=0; var pending=new TaskCompletionSource<HttpResponseMessage>();
        var setup=await Setup(request=> request.RequestUri!.AbsolutePath.EndsWith("parking-lots")?Task.FromResult(LotPage(Lot())):request.RequestUri.AbsolutePath.EndsWith("lookup")?Task.FromResult(Ok(Access())):Mutate());
        Task<HttpResponseMessage> Mutate(){posts++; return pending.Task;}
        var nav=new Navigation { Confirm=false }; var vm=new CheckInViewModel(setup.Api,setup.Lots,nav) { Entry=new(new(new("CARD"),Access()),Access().EligibleVehicles[0]) };
        await vm.ConfirmCommand.ExecuteAsync(null); Assert.Equal(0,posts); nav.Confirm=true; var first=vm.ConfirmCommand.ExecuteAsync(null); await vm.ConfirmCommand.ExecuteAsync(null); Assert.Equal(1,posts); Assert.False(vm.CanSubmit); pending.SetResult(Ok(Movement())); await first; Assert.True(vm.Completed); await vm.ConfirmCommand.ExecuteAsync(null); Assert.Equal(1,posts);
    }
    [Fact] public async Task EntryTimeoutVerifiesStateWithoutRetryingMutation()
    {
        var entered=false; var posts=0;
        var setup=await Setup(request=> request.RequestUri!.AbsolutePath.EndsWith("parking-lots")?Task.FromResult(LotPage(Lot())):request.RequestUri.AbsolutePath.EndsWith("lookup")?Task.FromResult(Ok(Access(entered?Movement():null))):Fail());
        Task<HttpResponseMessage> Fail(){posts++; entered=true; throw new TaskCanceledException();}
        var vm=new CheckInViewModel(setup.Api,setup.Lots,new Navigation()) { Entry=new(new(new("CARD"),Access()),Access().EligibleVehicles[0]) };
        await vm.ConfirmCommand.ExecuteAsync(null); Assert.Equal(1,posts); Assert.True(vm.Completed); Assert.Contains("Estado verificado",vm.ResultMessage); Assert.False(vm.CanSubmit);
    }
    [Fact] public async Task FailedTimeoutVerificationLocksEntryUntilExplicitVerification()
    {
        var posts=0; var lookups=0; var recover=false;
        var setup=await Setup(request=>request.RequestUri!.AbsolutePath.EndsWith("parking-lots")?Task.FromResult(LotPage(Lot())):request.RequestUri.AbsolutePath.EndsWith("lookup")?Lookup():Fail());
        Task<HttpResponseMessage> Lookup(){lookups++; return Task.FromResult(lookups==1||recover?Ok(Access()):new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));}
        Task<HttpResponseMessage> Fail(){posts++; throw new TaskCanceledException();}
        var vm=new CheckInViewModel(setup.Api,setup.Lots,new Navigation()) { Entry=new(new(new("CARD"),Access()),Access().EligibleVehicles[0]) }; await vm.ConfirmCommand.ExecuteAsync(null); Assert.True(vm.NeedsVerification); await vm.ConfirmCommand.ExecuteAsync(null); Assert.Equal(1,posts); recover=true; await vm.VerifyCommand.ExecuteAsync(null); Assert.False(vm.NeedsVerification); Assert.Equal(1,posts);
    }
    [Fact] public async Task StaleEntryIsBlockedBeforeConfirmation()
    { var setup=await Setup(r=>Task.FromResult(r.RequestUri!.AbsolutePath.EndsWith("parking-lots")?LotPage(Lot()):Ok(Access(status:"INACTIVE")))); var nav=new Navigation(); var vm=new CheckInViewModel(setup.Api,setup.Lots,nav) { Entry=new(new(new("CARD"),Access()),Access().EligibleVehicles[0]) }; await vm.ConfirmCommand.ExecuteAsync(null); Assert.True(vm.NeedsVerification); Assert.Equal(0,nav.ConfirmCalls); }
    [Fact] public async Task ExitTimeoutUsesPersistedDurationAndDoesNotResubmit()
    {
        var movement=Movement(); var posts=0; var closed=false;
        var setup=await Setup(r=>r.Method==HttpMethod.Get?Task.FromResult(Ok(new PagedResponse<ParkingMovementResponse>([closed?movement with { Status="CLOSED",CheckOutAtUtc=DateTimeOffset.UtcNow,Duration=TimeSpan.FromMinutes(73) }:movement],1,20,1,1))):Fail());
        Task<HttpResponseMessage> Fail(){posts++;closed=true;throw new TaskCanceledException();}
        var vm=new CheckOutViewModel(setup.Api,setup.Lots,new Navigation()) { Movement=movement }; await vm.ConfirmCommand.ExecuteAsync(null); Assert.Equal(1,posts); Assert.True(vm.Completed); Assert.Contains("01:13:00",vm.ResultMessage); await vm.ConfirmCommand.ExecuteAsync(null); Assert.Equal(1,posts);
    }
    [Fact] public async Task ClosedOldMovementCannotCloseNewMovement()
    { var old=Movement("CLOSED"); var setup=await Setup(_=>Task.FromResult(Ok(new PagedResponse<ParkingMovementResponse>([Movement(),old],1,20,2,1)))); var nav=new Navigation(); var vm=new CheckOutViewModel(setup.Api,setup.Lots,nav) { Movement=old }; await vm.ConfirmCommand.ExecuteAsync(null); Assert.True(vm.Completed); Assert.Equal(0,nav.ConfirmCalls); Assert.Equal(1,setup.Transport.Calls); }
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
    [Fact] public async Task CameraPermissionFinishingAfterPageLeavesDoesNotRestartScanner()
    { var pending=new TaskCompletionSource<bool>();var setup=await Setup(_=>throw new Exception());var vm=new GuardLookupViewModel(setup.Api,setup.Lots,new Navigation(),new PendingPermission(pending.Task));var start=vm.StartCommand.ExecuteAsync(null);vm.Stop();pending.SetResult(true);await start;Assert.False(vm.IsScanning);Assert.Equal(0,setup.Transport.Calls); }
    [Fact] public async Task IncidentFromMovementPreservesReferencesWithoutReentry()
    { string? body=null;var movement=Movement();var setup=await Setup(async r=>{if(r.RequestUri!.AbsolutePath.EndsWith("parking-lots"))return LotPage(Lot());body=await r.Content!.ReadAsStringAsync();return Ok(new IncidentCreatedResponse(Guid.NewGuid()));});await setup.Lots.RefreshAsync();var vm=new CreateIncidentViewModel(setup.Api,setup.Lots,new Picker(),new Navigation()) { Context=new(LotId,UserId,VehicleId,movement.MovementId,"Usuario","FRAME123","Principal"),Description="Daño" };await vm.SaveCommand.ExecuteAsync(null);Assert.True(vm.Completed);Assert.Contains(movement.MovementId.ToString(),body);Assert.Contains(UserId.ToString(),body);Assert.Contains(VehicleId.ToString(),body); }
    [Fact] public async Task UnknownExitStateBlocksPosting()
    { var setup=await Setup(_=>Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)));var vm=new CheckOutViewModel(setup.Api,setup.Lots,new Navigation()) { Movement=Movement() };await vm.ConfirmCommand.ExecuteAsync(null);Assert.True(vm.NeedsVerification);Assert.False(vm.CanSubmit);Assert.Equal(2,setup.Transport.Calls); }
    private sealed class PendingPermission(Task<bool> task):IScannerPermission { public Task<bool> RequestAsync()=>task; }
    private sealed class Transport(Func<HttpRequestMessage,Task<HttpResponseMessage>> send):HttpMessageHandler { public int Calls; protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken token){Calls++;return send(request);} }
    private sealed class Storage:ISecretStorage { private string? token;public Task<string?> GetAsync(string key)=>Task.FromResult(token);public Task SetAsync(string key,string value){token=value;return Task.CompletedTask;}public void Remove(string key)=>token=null; }
    private sealed class Store:IGuardSelectionStore { private readonly Dictionary<Guid,Guid?> values=[];public Guid? Get(Guid user)=>values.GetValueOrDefault(user);public void Set(Guid user,Guid? value)=>values[user]=value; }
    private sealed class Permission(bool allowed):IScannerPermission { public Task<bool> RequestAsync()=>Task.FromResult(allowed); }
    private sealed class Navigation:IUserNavigation { public string? Route;public bool Confirm=true;public int ConfirmCalls;public Task GoAsync(string route,IReadOnlyDictionary<string,object>? arguments=null){Route=route;return Task.CompletedTask;}public Task BackAsync()=>Task.CompletedTask;public Task MessageAsync(string title,string message)=>Task.CompletedTask;public Task<bool> ConfirmAsync(string title,string message){ConfirmCalls++;return Task.FromResult(Confirm);} }
    private sealed class Picker:IAttachmentPicker {public Task<PickedAttachment?> DocumentAsync()=>Task.FromResult<PickedAttachment?>(null);public Task<PickedAttachment?> PhotoAsync(bool camera)=>Task.FromResult<PickedAttachment?>(null);}
}
