using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using UniversityParking.Contracts.Common;
using UniversityParking.Contracts.Users;
using UniversityParking.Contracts.Universities;
using UniversityParking.Contracts.Vehicles;
using UniversityParking.Contracts.News;
using UniversityParking.Contracts.AcademicPeriods;
using UniversityParking.Contracts.ParkingLots;
using UniversityParking.Contracts.Incidents;
using UniversityParking.Contracts.Reporting;
using UniversityParking.Contracts.Parking;
using UniversityParking.Mobile.Core;
using UniversityParking.Mobile.ViewModels;

namespace UniversityParking.Mobile.Tests;

public sealed class AdminFeatureTests
{
    private static HttpResponseMessage Ok(object value)=>new(HttpStatusCode.OK){Content=JsonContent.Create(value)};
    private static HttpResponseMessage NoContent()=>new(HttpStatusCode.NoContent);
    private static UserListItemResponse User(string identification="123",string member="TEACHER",string status="ACTIVE")=>new(Guid.NewGuid(),identification,"Nombre",new Guid("a1100000-0000-4000-8000-000000000001"), "ETITC",null,member,"CARD",status,["USER"]);
    private static VehicleResponse Vehicle(string type="BICYCLE")=>new(Guid.NewGuid(),type,type=="BICYCLE"?null:"ABC123",type=="BICYCLE"?"FRAME123":null,"Brand","Model","Black","ACTIVE",Guid.NewGuid(),"Actual","ACTIVE",false,null);
    private static ParkingLotResponse Lot()=>new(Guid.NewGuid(),"Principal","Kennedy",new(6,0),new(22,0),"ACTIVE",[]);
    private static IncidentDetailResponse Incident(string status="OPEN")=>new(new(Guid.NewGuid(),Guid.NewGuid(),null,null,null,Guid.NewGuid(),"DAMAGE","Daño",status,DateTimeOffset.UtcNow,null,null,null,DateTimeOffset.UtcNow,DateTimeOffset.UtcNow),[]);
    private static NewsResponse News(string status="DRAFT")=>new(Guid.NewGuid(),"Título","Contenido",status,null,null,Guid.NewGuid(),DateTimeOffset.UtcNow,DateTimeOffset.UtcNow);
    private static HttpResponseMessage Users(params UserListItemResponse[] users)=>Ok(new PagedResponse<UserListItemResponse>(users,1,20,users.Length,users.Length>0?1:0));
    private static async Task<(AdminApiService Api,GuardApiService Uploads,AuthSession Session,Transport Transport)> Setup(Func<HttpRequestMessage,Task<HttpResponseMessage>> send,params string[] roles)
    {var session=new AuthSession(new Storage());await session.SaveAsync("token",new(Guid.NewGuid(),"ADMIN1","Admin",new Guid("a1100000-0000-4000-8000-000000000001"), "ETITC",null,"STAFF","CARD","ACTIVE",roles.Length==0?["USER","ADMIN"]:roles));var transport=new Transport(r=>r.Method==HttpMethod.Get&&r.RequestUri!.AbsolutePath=="/api/v1/universities"?Task.FromResult(Ok(Catalog())):r.Method==HttpMethod.Get&&r.RequestUri!.AbsolutePath.StartsWith("/api/v1/users/")&&Guid.TryParse(r.RequestUri!.Segments.Last(),out var id)?Task.FromResult(Ok(new UserProfileResponse(id,"123","Nombre",Catalog()[0].Id,"ETITC","Carrera","STUDENT","CARD","ACTIVE",["USER"]))):send(r));var api=new ApiClient(new HttpClient(transport){BaseAddress=new("https://test.example/")});return(new(api),new(api),session,transport);}
    [Theory][InlineData("USER")][InlineData("GUARD")]
    public async Task AdminListsRequireAdminBeforeCallingApi(string role)
    {var setup=await Setup(_=>throw new Exception(),role);var vm=new AdminUsersViewModel(setup.Api,setup.Session,new Navigation());await vm.LoadAsync();Assert.Contains("ADMIN",vm.ErrorMessage);Assert.Equal(0,setup.Transport.Calls);}
    [Fact]public async Task UserFiltersAreTypedAndEscaped()
    {string? query=null;var setup=await Setup(r=>{query=r.RequestUri!.Query;return Task.FromResult(Users());});var vm=new AdminUsersViewModel(setup.Api,setup.Session,new Navigation()){Search="x&role=ADMIN",SelectedType=new("STUDENT","Estudiante"),SelectedStatus=new("ACTIVE","Activo"),SelectedRole=new("GUARD","GUARD")};await vm.LoadAsync();Assert.Contains("%26",query);Assert.Contains("memberType=STUDENT",query);Assert.Contains("role=GUARD",query);Assert.True(vm.IsEmpty);}
    [Theory][InlineData("STUDENT",false)][InlineData("TEACHER",true)][InlineData("STAFF",true)]
    public async Task UserCareerIsRequiredOnlyForStudent(string member,bool valid)
    {var setup=await Setup(_=>Task.FromResult(Ok(new UserCreatedResponse(Guid.NewGuid()))));var vm=new AdminUserFormViewModel(setup.Api,setup.Session,new Navigation()){Identification="123",FullName="Nombre",CardCode="CARD",InitialPassword="Password1",Member=AdminPresentation.Members.Single(x=>x.Code==member)};await PrepareForm(vm);await vm.SaveCommand.ExecuteAsync(null);Assert.Equal(valid,vm.Completed);Assert.Equal(valid?2:1,setup.Transport.Calls);}
    [Fact]public async Task UserCreationAlwaysIncludesUserRoleAndClearsPassword()
    {string? body=null;var setup=await Setup(async r=>{body=await r.Content!.ReadAsStringAsync();return Ok(new UserCreatedResponse(Guid.NewGuid()));});var nav=new Navigation();var vm=new AdminUserFormViewModel(setup.Api,setup.Session,nav){Identification="123",FullName="Nombre",Career="Carrera",CardCode="CARD",InitialPassword="Password1",AddGuard=true,AddAdmin=true};await PrepareForm(vm);await vm.SaveCommand.ExecuteAsync(null);Assert.Contains("USER",body);Assert.Contains("GUARD",body);Assert.Contains("ADMIN",body);Assert.Equal("",vm.InitialPassword);Assert.Equal("admin-user-detail",nav.Route);}
    [Fact]public async Task EditUserDoesNotSendIdentificationPasswordOrRoles()
    {string? body=null;var setup=await Setup(async r=>{body=await r.Content!.ReadAsStringAsync();return NoContent();});var vm=new AdminUserFormViewModel(setup.Api,setup.Session,new Navigation()){UserId=Guid.NewGuid(),Identification="SHOULD_NOT_SEND",FullName="Nombre",Career="Carrera",CardCode="CARD"};await PrepareForm(vm);await vm.SaveCommand.ExecuteAsync(null);Assert.True(vm.Completed);Assert.DoesNotContain("identification",body!,StringComparison.OrdinalIgnoreCase);Assert.DoesNotContain("password",body!,StringComparison.OrdinalIgnoreCase);Assert.DoesNotContain("roles",body!,StringComparison.OrdinalIgnoreCase);}
    [Fact]public async Task CreateUserDoubleTapCreatesOnce()
    {var pending=new TaskCompletionSource<HttpResponseMessage>();var setup=await Setup(_=>pending.Task);var vm=new AdminUserFormViewModel(setup.Api,setup.Session,new Navigation()){Identification="123",FullName="Nombre",Career="Carrera",CardCode="CARD",InitialPassword="Password1"};await PrepareForm(vm);var first=vm.SaveCommand.ExecuteAsync(null);await vm.SaveCommand.ExecuteAsync(null);Assert.Equal(2,setup.Transport.Calls);Assert.False(vm.CanSave);pending.SetResult(Ok(new UserCreatedResponse(Guid.NewGuid())));await first;Assert.False(vm.CanSave);}
    [Fact]public async Task UserRoleCannotBeRemovedEvenIfInjected()
    {var setup=await Setup(_=>throw new Exception());var vm=new AdminUserDetailViewModel(setup.Api,setup.Session,new Navigation(),new AppNavigation()){UserId=Guid.NewGuid(),User=setup.Session.User,SelectedRole=new("USER","USER")};await vm.RemoveRoleCommand.ExecuteAsync(null);Assert.Contains("USER",vm.ErrorMessage);Assert.Equal(0,setup.Transport.Calls);}

    [Fact]public async Task MissingUniversityIdPreventsSubmission()
    {var setup=await Setup(_=>throw new Exception("No debe enviar"));var vm=new AdminUserFormViewModel(setup.Api,setup.Session,new Navigation()){Identification="123",FullName="Nombre",Career="Ingeniería",CardCode="CARD",InitialPassword="Password1"};await vm.SaveCommand.ExecuteAsync(null);Assert.Contains("universidad",vm.ErrorMessage);Assert.Equal(0,setup.Transport.Calls);}

    [Fact]public async Task UniversityReferenceIsSerializedAsGuidWithoutFreeText()
    {string? body=null;var setup=await Setup(async r=>{body=await r.Content!.ReadAsStringAsync();return Ok(new UserCreatedResponse(Guid.NewGuid()));});var id=Catalog()[0].Id;var vm=new AdminUserFormViewModel(setup.Api,setup.Session,new Navigation()){Identification="123",FullName="Nombre",Career="Ingeniería",CardCode="CARD",InitialPassword="Password1"};await PrepareForm(vm);await vm.SaveCommand.ExecuteAsync(null);Assert.True(vm.Completed);using var json=JsonDocument.Parse(body!);Assert.Equal(id,json.RootElement.GetProperty("universityId").GetGuid());Assert.False(json.RootElement.TryGetProperty("university",out _));Assert.False(json.RootElement.TryGetProperty("universityName",out _));}
    [Fact]public async Task ChangingOwnRolesClearsSessionForNewJwtClaims()
    {var setup=await Setup(_=>Task.FromResult(NoContent()));var appNav=new AppNavigation();var vm=new AdminUserDetailViewModel(setup.Api,setup.Session,new Navigation(),appNav){UserId=setup.Session.User!.Id,User=setup.Session.User,SelectedRole=new("GUARD","GUARD")};await vm.AssignRoleCommand.ExecuteAsync(null);Assert.Null(setup.Session.User);Assert.Null(await setup.Session.GetTokenAsync());Assert.True(appNav.LoginShown);}
    [Fact]public async Task DeactivationCancellationDoesNotWrite()
    {var setup=await Setup(_=>throw new Exception());var vm=new AdminUserDetailViewModel(setup.Api,setup.Session,new Navigation{Confirm=false},new AppNavigation()){UserId=Guid.NewGuid(),User=setup.Session.User};await vm.StatusCommand.ExecuteAsync(null);Assert.Equal(0,setup.Transport.Calls);}
    [Fact]public async Task ExactUserLookupDoesNotChoosePartialSearchMatch()
    {var exact=User("123");var setup=await Setup(r=>Task.FromResult(Ok(new PagedResponse<UserListItemResponse>(r.RequestUri!.Query.Contains("page=2")?[exact]:[User("1234")],r.RequestUri.Query.Contains("page=2")?2:1,20,21,2))));var found=await setup.Api.FindUserAsync("123");Assert.Equal(exact.Id,found.Value!.Id);Assert.Equal(2,setup.Transport.Calls);}
    [Theory][InlineData("STUDENT","CAR","ACTIVE")][InlineData("TEACHER","BICYCLE","INACTIVE")]
    public async Task TransferRejectsInvalidNewOwnerLocally(string member,string type,string status)
    {var setup=await Setup(_=>Task.FromResult(Users(User(member:member,status:status))));var vm=new AdminTransferViewModel(setup.Api,setup.Session,new Navigation()){Vehicle=Vehicle(type),Identification="123",Reason="Cambio"};await vm.SaveCommand.ExecuteAsync(null);Assert.False(vm.Completed);Assert.NotEmpty(vm.ErrorMessage);Assert.Equal(1,setup.Transport.Calls);}
    [Fact]public async Task TransferHasExplicitWarningAndMinimalRequest()
    {string? body=null;var owner=User();var setup=await Setup(async r=>{if(r.Method==HttpMethod.Get)return Users(owner);body=await r.Content!.ReadAsStringAsync();return NoContent();});var nav=new Navigation();var vm=new AdminTransferViewModel(setup.Api,setup.Session,nav){Vehicle=Vehicle(),Identification="123",Reason="Cambio"};await vm.SaveCommand.ExecuteAsync(null);Assert.True(vm.Completed);Assert.Contains("cancelará el registro",nav.Confirmation);Assert.Contains("renovar",nav.Confirmation);using var json=JsonDocument.Parse(body!);Assert.Equal("123",json.RootElement.GetProperty("newOwnerIdentificationNumber").GetString());Assert.False(json.RootElement.TryGetProperty("newOwnerId",out _));Assert.Equal(2,json.RootElement.EnumerateObject().Count());}
    [Fact]public async Task UncertainTransferCannotBeSilentlyRepeated()
    {var writes=0;var setup=await Setup(r=>r.Method==HttpMethod.Get?Task.FromResult(Users(User())):Fail());Task<HttpResponseMessage> Fail(){writes++;throw new TaskCanceledException();}var vm=new AdminTransferViewModel(setup.Api,setup.Session,new Navigation()){Vehicle=Vehicle(),Identification="123",Reason="Cambio"};await vm.SaveCommand.ExecuteAsync(null);Assert.True(vm.WriteUncertain);await vm.SaveCommand.ExecuteAsync(null);Assert.Equal(1,writes);}
    [Fact]public async Task IdentifierCorrectionNeverChangesTypeAndUsesPatch()
    {string? body=null;HttpMethod? method=null;var setup=await Setup(async r=>{body=await r.Content!.ReadAsStringAsync();method=r.Method;return NoContent();});var vm=new AdminCorrectViewModel(setup.Api,setup.Session,new Navigation()){Vehicle=Vehicle(),Identifier="FRAME456",Reason="Error de digitación"};await vm.SaveCommand.ExecuteAsync(null);Assert.Equal(HttpMethod.Patch,method);Assert.DoesNotContain("type",body!,StringComparison.OrdinalIgnoreCase);Assert.Contains("identifier",body);Assert.True(vm.Completed);}
    [Fact]public async Task ClosedPeriodCannotBeReactivated()
    {var setup=await Setup(_=>throw new Exception());var vm=new AdminPeriodFormViewModel(setup.Api,setup.Session,new Navigation()){Period=new(Guid.NewGuid(),"2026-2",new(2026,7,1),new(2026,12,31),"CLOSED")};Assert.False(vm.CanActivate);Assert.False(vm.CanClose);await vm.ActivateCommand.ExecuteAsync(null);Assert.Equal(0,setup.Transport.Calls);}
    [Fact]public async Task PeriodCreationRejectsReversedDates()
    {var setup=await Setup(_=>throw new Exception());var vm=new AdminPeriodFormViewModel(setup.Api,setup.Session,new Navigation()){Name="Periodo",StartsOn=new(2026,12,31),EndsOn=new(2026,7,1)};await vm.SaveCommand.ExecuteAsync(null);Assert.Contains("fecha final",vm.ErrorMessage);Assert.Equal(0,setup.Transport.Calls);}
    [Fact]public async Task LotCreationDoesNotSendZonesOrCapacity()
    {string? body=null;var setup=await Setup(async r=>{body=await r.Content!.ReadAsStringAsync();return Ok(new ParkingLotCreatedResponse(Guid.NewGuid()));});var vm=new AdminLotFormViewModel(setup.Api,setup.Session,new Navigation()){Name="Principal",Campus="Kennedy"};await vm.SaveCommand.ExecuteAsync(null);Assert.True(vm.Completed);Assert.DoesNotContain("zones",body!,StringComparison.OrdinalIgnoreCase);Assert.DoesNotContain("capacity",body!,StringComparison.OrdinalIgnoreCase);Assert.Contains("openingTime",body);}
    [Fact]public async Task LotDeactivationRequiresConfirmation()
    {var setup=await Setup(_=>throw new Exception());var vm=new AdminLotFormViewModel(setup.Api,setup.Session,new Navigation{Confirm=false}){Lot=Lot()};await vm.StatusCommand.ExecuteAsync(null);Assert.Equal(0,setup.Transport.Calls);}
    [Fact]public async Task SavingNewsCreatesDraftWithoutPublishing()
    {string? path=null;var setup=await Setup(r=>{path=r.RequestUri!.AbsolutePath;return Task.FromResult(Ok(new NewsCreatedResponse(Guid.NewGuid())));});var vm=new AdminNewsFormViewModel(setup.Api,setup.Session,new Navigation()){Title="Título",Content="Contenido"};await vm.SaveCommand.ExecuteAsync(null);Assert.Equal("/api/v1/admin/news",path);Assert.Equal(1,setup.Transport.Calls);Assert.Equal("DRAFT",vm.State);}
    [Theory][InlineData(true)][InlineData(false)]
    public async Task PublishingAndArchivingRequireConfirmation(bool publish)
    {var setup=await Setup(_=>throw new Exception());var vm=new AdminNewsFormViewModel(setup.Api,setup.Session,new Navigation{Confirm=false}){News=News()};await (publish?vm.PublishCommand:vm.ArchiveCommand).ExecuteAsync(null);Assert.Equal(0,setup.Transport.Calls);}
    [Fact]public async Task ArchivedNewsIsReadOnly()
    {var setup=await Setup(_=>throw new Exception());var vm=new AdminNewsFormViewModel(setup.Api,setup.Session,new Navigation()){News=News("ARCHIVED")};Assert.False(vm.CanEditNews);Assert.False(vm.CanPublish);Assert.False(vm.CanArchive);await vm.SaveCommand.ExecuteAsync(null);Assert.Equal(0,setup.Transport.Calls);}
    [Fact]public async Task IncidentResolutionIsRequiredAndConfirmsBeforeWriting()
    {var detail=Incident();var writes=0;var setup=await Setup(r=>{if(r.Method==HttpMethod.Get)return Task.FromResult(Ok(detail));writes++;return Task.FromResult(NoContent());});var nav=new Navigation{Confirm=false};var vm=new AdminIncidentDetailViewModel(setup.Api,setup.Session,nav,new Viewer()){IncidentId=detail.Incident.Id};await vm.LoadCommand.ExecuteAsync(null);await vm.ResolveCommand.ExecuteAsync(null);Assert.Contains("obligatorio",vm.ErrorMessage);vm.Resolution="Solución";await vm.ResolveCommand.ExecuteAsync(null);Assert.Equal(0,writes);nav.Confirm=true;detail=detail with{Incident=detail.Incident with{Status="RESOLVED",Resolution="Solución"}};await vm.ResolveCommand.ExecuteAsync(null);Assert.Equal(1,writes);Assert.False(vm.IsOpen);}
    [Fact]public async Task UncertainIncidentResolutionLocksUntilReload()
    {var detail=Incident();var writes=0;var setup=await Setup(r=>r.Method==HttpMethod.Get?Task.FromResult(Ok(detail)):Fail());Task<HttpResponseMessage> Fail(){writes++;throw new TaskCanceledException();}var vm=new AdminIncidentDetailViewModel(setup.Api,setup.Session,new Navigation(),new Viewer()){IncidentId=detail.Incident.Id,Resolution="Solución"};await vm.LoadCommand.ExecuteAsync(null);await vm.ResolveCommand.ExecuteAsync(null);Assert.True(vm.WriteUncertain);await vm.ResolveCommand.ExecuteAsync(null);Assert.Equal(1,writes);detail=detail with{Incident=detail.Incident with{Status="RESOLVED"}};await vm.LoadCommand.ExecuteAsync(null);Assert.False(vm.WriteUncertain);Assert.False(vm.IsOpen);}
    [Fact]public async Task AdminOnlyCanCreateIncidentWithoutGuardRole()
    {var lot=Lot();var setup=await Setup(r=>Task.FromResult(r.Method==HttpMethod.Get?Ok(new PagedResponse<ParkingLotResponse>([lot],1,20,1,1)):Ok(new IncidentCreatedResponse(Guid.NewGuid()))));var vm=new AdminIncidentCreateViewModel(setup.Api,setup.Uploads,setup.Session,new Picker(),new Navigation()){Description="Descripción"};await vm.LoadCommand.ExecuteAsync(null);await vm.SaveCommand.ExecuteAsync(null);Assert.True(vm.Completed);}
    [Theory][InlineData("DAILY","/reports/access/daily")][InlineData("VEHICLE_TYPE","/reports/access/by-vehicle-type")][InlineData("MEMBER_TYPE","/reports/access/by-member-type")]
    public async Task ReportsUseTheirRealEndpoint(string kind,string expected)
    {string? path=null;var setup=await Setup(r=>{path=r.RequestUri!.AbsolutePath;return Task.FromResult(kind=="DAILY"?Ok(new[]{new DailyAccessResponse(new(2026,10,7),3,2)}):Ok(new[]{new AccessGroupResponse(kind=="VEHICLE_TYPE"?"BICYCLE":"STUDENT",3,2)}));});var vm=new AdminReportsViewModel(setup.Api,setup.Session){SelectedReport=new(kind,kind)};await vm.LoadAsync();Assert.EndsWith(expected,path);Assert.Contains("Entradas: 3",Assert.Single(vm.Items).Summary);}
    [Fact]public async Task VehicleHistoryPaginatesEverySectionWithoutLosingRegistrations()
    {var id=Guid.NewGuid();var history=new VehicleHistoryResponse(id,new([],1,20,0,0),new([new(Guid.NewGuid(),id,Guid.NewGuid(),Guid.NewGuid(),"ACTIVE",DateTimeOffset.UtcNow,null)],1,20,25,2),new([],1,20,0,0),new([],1,20,0,0));var setup=await Setup(_=>Task.FromResult(Ok(history)));var vm=new AdminReportsViewModel(setup.Api,setup.Session){SelectedReport=new("VEHICLE_HISTORY","Historial"),TargetVehicleId=id};await vm.LoadAsync();Assert.Equal(2,vm.TotalPages);Assert.True(vm.CanNext);Assert.Equal("Registro académico",Assert.Single(vm.Items).Title);}
    [Fact]public async Task ReportFiltersRejectReversedDates()
    {var setup=await Setup(_=>throw new Exception());var vm=new AdminReportsViewModel(setup.Api,setup.Session){DateFrom=new(2026,10,8),DateTo=new(2026,10,7)};await vm.LoadAsync();Assert.Contains("fecha final",vm.ErrorMessage);Assert.Equal(0,setup.Transport.Calls);}
    [Fact]public async Task AdminDoesNotGetGuardActionsImplicitly()
    {var setup=await Setup(_=>throw new Exception());var nav=new Navigation();var auth=new AuthService(new ApiClient(new HttpClient(new Transport(_=>throw new Exception()))),setup.Session,new AppNavigation());var vm=new AdminDashboardViewModel(setup.Api,setup.Session,nav,auth);Assert.False(vm.HasGuard);await vm.GuardCommand.ExecuteAsync(null);Assert.Null(nav.Route);Assert.Contains("GUARD",vm.ErrorMessage);}
    [Fact]public void AuditValuesAreReadableAndHandleNonJson(){Assert.Equal("status: ACTIVE\ncount: 2",AdminPresentation.AuditValues("{\"status\":\"ACTIVE\",\"count\":2}"));Assert.Equal("texto",AdminPresentation.AuditValues("texto"));Assert.Equal("Sin valores",AdminPresentation.AuditValues(null));}
    [Fact]public async Task RemovedRoleUsesDeleteAndNoAutomaticRetry()
    {HttpMethod? method=null;var setup=await Setup(r=>{method=r.Method;throw new TaskCanceledException();});var result=await setup.Api.RemoveRoleAsync(Guid.NewGuid(),"GUARD");Assert.Equal("REQUEST_TIMEOUT",result.Error!.Code);Assert.Equal(HttpMethod.Delete,method);Assert.Equal(1,setup.Transport.Calls);}
    [Fact]public async Task NewsWithUnsavedEditsCannotPublishOldContent()
    {var setup=await Setup(_=>throw new Exception());var vm=new AdminNewsFormViewModel(setup.Api,setup.Session,new Navigation()){News=News()};Assert.True(vm.CanPublish);vm.Content="Cambio sin guardar";Assert.False(vm.CanPublish);await vm.PublishCommand.ExecuteAsync(null);Assert.Equal(0,setup.Transport.Calls);}
    [Fact]public async Task ClearedLotPickerDoesNotBreakHistoryInitialization()
    {var setup=await Setup(r=>Task.FromResult(r.RequestUri!.AbsolutePath.EndsWith("parking-lots")?Ok(new PagedResponse<ParkingLotResponse>([Lot()],1,20,1,1)):Ok(new PagedResponse<ParkingMovementResponse>([],1,20,0,0))));var vm=new AdminHistoryViewModel(setup.Api,setup.Session,new Navigation()){SelectedLot=null!};await vm.LoadAsync();Assert.Equal("",vm.ErrorMessage);Assert.NotNull(vm.SelectedLot);Assert.True(vm.IsEmpty);}
    [Fact]public async Task ClearedReportPickerShowsValidationInsteadOfThrowing()
    {var setup=await Setup(_=>throw new Exception());var vm=new AdminReportsViewModel(setup.Api,setup.Session){SelectedReport=null!};await vm.LoadAsync();Assert.Contains("Selecciona un reporte",vm.ErrorMessage);Assert.Equal(0,setup.Transport.Calls);}
    private static UniversityResponse[] Catalog()=>[
        new(new Guid("a1100000-0000-4000-8000-000000000001"),"ETITC","ETITC"),
        new(new Guid("a1100000-0000-4000-8000-000000000002"),"CMC","Colegio Mayor de Cundinamarca"),
        new(new Guid("a1100000-0000-4000-8000-000000000003"),"UPN","U. Pedagógica")];
    private static async Task PrepareForm(AdminUserFormViewModel vm)
    {await vm.LoadCommand.ExecuteAsync(null);vm.SelectedUniversity=vm.Universities.First();}
    private sealed class Transport(Func<HttpRequestMessage,Task<HttpResponseMessage>> send):HttpMessageHandler{public int Calls;protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage r,CancellationToken token){Calls++;return send(r);}}
    private sealed class Storage:ISecretStorage{private string? token;public Task<string?> GetAsync(string key)=>Task.FromResult(token);public Task SetAsync(string key,string value){token=value;return Task.CompletedTask;}public void Remove(string key)=>token=null;}
    private sealed class Navigation:IUserNavigation{public bool Confirm=true;public string? Confirmation;public string? Route;public Task GoAsync(string route,IReadOnlyDictionary<string,object>? arguments=null){Route=route;return Task.CompletedTask;}public Task BackAsync()=>Task.CompletedTask;public Task MessageAsync(string title,string message)=>Task.CompletedTask;public Task<bool> ConfirmAsync(string title,string message){Confirmation=message;return Task.FromResult(Confirm);}}
    private sealed class AppNavigation:IAppNavigation{public bool LoginShown;public Task ShowLoginAsync(string? message=null){LoginShown=true;return Task.CompletedTask;}public Task ShowAuthenticatedAsync()=>Task.CompletedTask;}
    private sealed class Viewer:IFileViewer{public Task OpenAsync(string name,string mime,byte[] bytes)=>Task.CompletedTask;public void ClearCache(){}}
    private sealed class Picker:IAttachmentPicker{public Task<PickedAttachment?> DocumentAsync()=>Task.FromResult<PickedAttachment?>(null);public Task<PickedAttachment?> PhotoAsync(bool camera)=>Task.FromResult<PickedAttachment?>(null);}
}
