using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using UniversityParking.Mobile.Core;
using UniversityParking.Contracts.Common;
using UniversityParking.Contracts.Users;
using UniversityParking.Contracts.Vehicles;
using UniversityParking.Contracts.AcademicPeriods;
using UniversityParking.Contracts.ParkingLots;
using UniversityParking.Contracts.Parking;
using UniversityParking.Contracts.Incidents;
using UniversityParking.Contracts.News;
using UniversityParking.Contracts.Reporting;

namespace UniversityParking.Mobile.ViewModels;

public sealed record AdminRow(string Title,string Summary,object Value);
public sealed record LotOption(Guid? Id,string Name);
public abstract partial class AdminListViewModel(AdminApiService api,IAuthSession session,IUserNavigation navigation) : PagedUserViewModel<AdminRow>
{
    protected AdminApiService Api => api; protected IAuthSession Session => session; protected IUserNavigation Navigation => navigation;
    [ObservableProperty] private string search="";
    [ObservableProperty] private TypeChoice selectedType=new("","Todos");
    [ObservableProperty] private TypeChoice selectedStatus=new("","Todos");
    [ObservableProperty] private DateTime dateFrom=MobileDates.Today.AddDays(-30);
    [ObservableProperty] private DateTime dateTo=MobileDates.Today;
    [ObservableProperty] private LotOption selectedLot=new(null,"Todos los parqueaderos");
    public ObservableCollection<LotOption> Lots {get;}=[];
    public virtual IReadOnlyList<TypeChoice> Types => [];
    public virtual IReadOnlyList<TypeChoice> States => [];
    public Task LoadAsync()=>LoadPageAsync();
    protected void Require()=>AdminPresentation.RequireAdmin(session);
    protected void Dates(){if(DateTo.Date<DateFrom.Date)throw new UserInputException("La fecha final debe ser mayor o igual a la inicial.");}
    protected async Task LoadLotsAsync()
    {
        if(Lots.Count>0)return;var values=new List<LotOption>{new(null,"Todos los parqueaderos")};
        for(var page=1;;page++){var result=await api.LotsAsync(null,page);if(!result.IsSuccess)throw new UserInputException(result.Error!.Message);values.AddRange(result.Value!.Items.Select(x=>new LotOption(x.Id,x.Name)));if(page>=result.Value.TotalPages)break;}
        Lots.Clear();foreach(var value in values)Lots.Add(value);SelectedLot=Lots.FirstOrDefault(x=>x.Id==SelectedLot?.Id)??Lots[0];
    }
    protected bool Rows<T>(ApiResult<PagedResponse<T>> result,Func<T,AdminRow> row)
    {if(!Accepted(result))return false;var value=result.Value!;Apply(new(value.Items.Select(row).ToArray(),value.Page,value.PageSize,value.TotalCount,value.TotalPages));return true;}
    protected override Task LoadPageAsync()=>WorkAsync(async()=>{Require();await ReadAsync();});
    protected abstract Task ReadAsync();
    protected abstract Task OpenAsync(AdminRow row);
    [RelayCommand] private Task OpenRowAsync(AdminRow? row)=>WorkAsync(async()=>{Require();if(row is not null)await OpenAsync(row);});
}
public partial class AdminDashboardViewModel(AdminApiService api,IAuthSession session,IUserNavigation navigation,AuthService auth):UserFeatureViewModel
{
    [ObservableProperty] private AdminDashboardResponse? dashboard;
    public string Summary=>Dashboard is null?"":$"Usuarios activos: {Dashboard.ActiveUsers}\nVehículos activos: {Dashboard.ActiveVehicles}\nDentro: {Dashboard.VehiclesInside}\nEntradas hoy: {Dashboard.TodayCheckIns}\nSalidas hoy: {Dashboard.TodayCheckOuts}\nIncidentes abiertos: {Dashboard.OpenIncidents}\nPeriodo actual: {Dashboard.CurrentAcademicPeriod?.Name??"Sin periodo activo"}";
    public bool HasGuard=>session.User?.Roles.Contains("GUARD")==true;
    partial void OnDashboardChanged(AdminDashboardResponse? value)=>OnPropertyChanged(nameof(Summary));
    [RelayCommand] private Task LoadAsync()=>WorkAsync(async()=>{AdminPresentation.RequireAdmin(session);Dashboard=null;var result=await api.DashboardAsync();if(Accepted(result))Dashboard=result.Value;});
    [RelayCommand] private Task OpenAsync(string? route)=>WorkAsync(async()=>{AdminPresentation.RequireAdmin(session);if(route is not null)await navigation.GoAsync(route);});
    [RelayCommand] private Task GuardAsync()=>WorkAsync(async()=>{AdminPresentation.RequireAdmin(session);if(!HasGuard)throw new UserInputException("Se requiere GUARD para registrar ingresos y salidas.");await navigation.GoAsync("guard-home");});
    [RelayCommand] private Task LogoutAsync()=>WorkAsync(auth.LogoutAsync);
}
public partial class AdminUsersViewModel(AdminApiService api,IAuthSession session,IUserNavigation navigation):AdminListViewModel(api,session,navigation)
{
    public override IReadOnlyList<TypeChoice> Types=>AdminPresentation.All(AdminPresentation.Members);
    public override IReadOnlyList<TypeChoice> States=>AdminPresentation.All(AdminPresentation.Statuses);
    public IReadOnlyList<TypeChoice> Roles=>AdminPresentation.All(AdminPresentation.Roles);
    [ObservableProperty] private TypeChoice selectedRole=new("","Todos");
    protected override async Task ReadAsync()=>Rows(await Api.UsersAsync(Search,SelectedType?.Code,SelectedStatus?.Code,SelectedRole?.Code,Page),x=>new(x.FullName,$"{x.IdentificationNumber} · {AdminPresentation.Member(x.MemberType)} · {AdminPresentation.Status(x.Status)}\n{x.UniversityName}\nRoles: {string.Join(", ",x.Roles)}",x));
    protected override Task OpenAsync(AdminRow row)=>Navigation.GoAsync("admin-user-detail",new Dictionary<string,object>{["userId"]=((UserListItemResponse)row.Value).Id});
    [RelayCommand] private Task CreateAsync()=>WorkAsync(()=>{Require();return Navigation.GoAsync("admin-user-form");});
}
public partial class AdminVehiclesViewModel(AdminApiService api,IAuthSession session,IUserNavigation navigation):AdminListViewModel(api,session,navigation)
{
    public override IReadOnlyList<TypeChoice> Types=>GuardPresentation.VehicleTypes;
    public override IReadOnlyList<TypeChoice> States=>AdminPresentation.All(AdminPresentation.Statuses);
    public IReadOnlyList<TypeChoice> Registrations=>AdminPresentation.All(Enum.GetNames<VehicleRegistrationState>().Select(x=>new TypeChoice(x,VehiclePresentation.RegistrationName(x))));
    [ObservableProperty] private TypeChoice selectedRegistration=new("","Todos");
    [ObservableProperty] private string ownerIdentification="";
    protected override async Task ReadAsync()=>Rows(await Api.VehiclesAsync(Search,SelectedType?.Code,SelectedStatus?.Code,OwnerIdentification,SelectedRegistration?.Code,Page),VehicleRow);
    public static AdminRow VehicleRow(VehicleResponse x)=>new($"{AdminPresentation.Identifier(x)} · {x.Brand} {x.Model}",$"{VehiclePresentation.TypeName(x.Type)} · {x.Color} · {AdminPresentation.Status(x.Status)}\nPropietario: {x.CurrentOwnerFullName??"Sin propietario"}\nRegistro: {VehiclePresentation.RegistrationName(x.RegistrationState)} · {(x.IsInside?"Dentro":"Fuera")}",x);
    protected override Task OpenAsync(AdminRow row)=>Navigation.GoAsync("admin-vehicle-detail",new Dictionary<string,object>{["vehicleId"]=((VehicleResponse)row.Value).Id});
}
public partial class AdminPeriodsViewModel(AdminApiService api,IAuthSession session,IUserNavigation navigation):AdminListViewModel(api,session,navigation)
{
    public override IReadOnlyList<TypeChoice> States {get;}=[new("","Todos"),new("PLANNED","Planeado"),new("ACTIVE","Activo"),new("CLOSED","Cerrado")];
    protected override async Task ReadAsync()
    {
        var result=await Api.PeriodsAsync();if(!Accepted(result))return;var all=result.Value!.Where(x=>(string.IsNullOrEmpty(SelectedStatus?.Code)||x.Status==SelectedStatus?.Code)&&(Search==""||x.Name.Contains(Search.Trim(),StringComparison.OrdinalIgnoreCase))).ToArray();
        var total=(all.Length+19)/20;Page=Math.Min(Page,Math.Max(1,total));Apply(new(all.Skip((Page-1)*20).Take(20).Select(x=>new AdminRow(x.Name,$"{x.StartsOn:dd/MM/yyyy} — {x.EndsOn:dd/MM/yyyy}\n{States.FirstOrDefault(s=>s.Code==x.Status)?.Label??x.Status}",x)).ToArray(),Page,20,all.Length,total));
    }
    protected override Task OpenAsync(AdminRow row)=>Navigation.GoAsync("admin-period-form",new Dictionary<string,object>{["period"]=(AcademicPeriodResponse)row.Value});
    [RelayCommand] private Task CreateAsync()=>WorkAsync(()=>{Require();return Navigation.GoAsync("admin-period-form");});
}
public partial class AdminLotsViewModel(AdminApiService api,IAuthSession session,IUserNavigation navigation):AdminListViewModel(api,session,navigation)
{
    public override IReadOnlyList<TypeChoice> States=>AdminPresentation.All(AdminPresentation.Statuses);
    protected override async Task ReadAsync()=>Rows(await Api.LotsAsync(SelectedStatus?.Code,Page),x=>new(x.Name,$"{x.Campus}\n{ x.OpeningTime:HH:mm} — {x.ClosingTime:HH:mm} · {AdminPresentation.Status(x.Status)}",x));
    protected override Task OpenAsync(AdminRow row)=>Navigation.GoAsync("admin-lot-form",new Dictionary<string,object>{["lot"]=(ParkingLotResponse)row.Value});
    [RelayCommand] private Task CreateAsync()=>WorkAsync(()=>{Require();return Navigation.GoAsync("admin-lot-form");});
}
public partial class AdminIncidentsViewModel(AdminApiService api,IAuthSession session,IUserNavigation navigation):AdminListViewModel(api,session,navigation)
{
    public override IReadOnlyList<TypeChoice> Types=>AdminPresentation.All(GuardPresentation.IncidentTypes);
    public override IReadOnlyList<TypeChoice> States {get;}=[new("","Todos"),new("OPEN","Abierto"),new("RESOLVED","Resuelto"),new("CANCELLED","Cancelado")];
    [ObservableProperty] private string identification="";
    [ObservableProperty] private string vehicleIdentifier="";
    protected override async Task ReadAsync()
    {
        Dates();await LoadLotsAsync();Guid? user=null,vehicle=null;
        if(!string.IsNullOrWhiteSpace(Identification)){var result=await Api.FindUserAsync(Identification);if(!Accepted(result))return;user=result.Value!.Id;}
        if(!string.IsNullOrWhiteSpace(VehicleIdentifier)){var result=await Api.FindVehicleAsync(VehicleIdentifier);if(!Accepted(result))return;vehicle=result.Value!.Id;}
        Rows(await Api.IncidentsAsync(SelectedLot?.Id,user,vehicle,SelectedType?.Code,SelectedStatus?.Code,DateOnly.FromDateTime(DateFrom),DateOnly.FromDateTime(DateTo),Page),x=>new(GuardPresentation.IncidentType(x.Type)+" · "+GuardPresentation.IncidentStatus(x.Status),$"{MobileDates.Display(x.OccurredAt)}\n{Lots.FirstOrDefault(l=>l.Id==x.ParkingLotId)?.Name}\n{x.Description}\nUsuario: {x.UserId}\nVehículo: {x.VehicleId}",x));
    }
    protected override Task OpenAsync(AdminRow row)=>Navigation.GoAsync("admin-incident-detail",new Dictionary<string,object>{["incidentId"]=((IncidentResponse)row.Value).Id});
    [RelayCommand] private Task CreateAsync()=>WorkAsync(()=>{Require();return Navigation.GoAsync("admin-incident-create");});
}
public partial class AdminNewsViewModel(AdminApiService api,IAuthSession session,IUserNavigation navigation):AdminListViewModel(api,session,navigation)
{
    public override IReadOnlyList<TypeChoice> States {get;}=[new("","Todos"),new("DRAFT","Borrador"),new("PUBLISHED","Publicada"),new("ARCHIVED","Archivada")];
    protected override async Task ReadAsync()=>Rows(await Api.NewsAsync(SelectedStatus?.Code,Search,Page),x=>new(x.Title,$"{States.FirstOrDefault(s=>s.Code==x.Status)?.Label}\n{x.Content[..Math.Min(x.Content.Length,160)]}",x));
    protected override Task OpenAsync(AdminRow row)=>Navigation.GoAsync("admin-news-form",new Dictionary<string,object>{["news"]=(NewsResponse)row.Value});
    [RelayCommand] private Task CreateAsync()=>WorkAsync(()=>{Require();return Navigation.GoAsync("admin-news-form");});
}
public partial class AdminHistoryViewModel(AdminApiService api,IAuthSession session,IUserNavigation navigation):AdminListViewModel(api,session,navigation)
{
    public override IReadOnlyList<TypeChoice> Types=>GuardPresentation.VehicleTypes;
    public override IReadOnlyList<TypeChoice> States {get;}=[new("","Todos"),new("OPEN","Dentro"),new("CLOSED","Salida registrada")];
    [ObservableProperty] private string identification="";
    [ObservableProperty] private string plate="";
    [ObservableProperty] private string frame="";
    protected override async Task ReadAsync()
    {
        Dates();await LoadLotsAsync();Rows(await Api.HistoryAsync(SelectedLot?.Id,Identification,Plate,Frame,SelectedType?.Code,SelectedStatus?.Code,DateOnly.FromDateTime(DateFrom),DateOnly.FromDateTime(DateTo),Page),x=>new($"{x.UserFullName} · {x.VehicleIdentifier}",$"{x.ParkingLotName} · {VehiclePresentation.TypeName(x.VehicleType)}\nEntrada: {MobileDates.Display(x.CheckInAtUtc)}\n{(x.CheckOutAtUtc is null?"Salida pendiente":"Salida: "+MobileDates.Display(x.CheckOutAtUtc.Value))}\nDuración API: {x.Duration}",x));
    }
    protected override Task OpenAsync(AdminRow row)=>Navigation.MessageAsync("Movimiento",row.Title+"\n"+row.Summary);
}
public partial class AdminAuditViewModel(AdminApiService api,IAuthSession session,IUserNavigation navigation):AdminListViewModel(api,session,navigation)
{
    [ObservableProperty] private string actorIdentification="";
    [ObservableProperty] private string action="";
    [ObservableProperty] private string entityType="";
    [ObservableProperty] private string entityId="";
    protected override async Task ReadAsync()
    {
        Dates();Guid? actor=null,id=null;if(!string.IsNullOrWhiteSpace(ActorIdentification)){var result=await Api.FindUserAsync(ActorIdentification);if(!Accepted(result))return;actor=result.Value!.Id;}
        if(!string.IsNullOrWhiteSpace(EntityId)){if(!Guid.TryParse(EntityId.Trim(),out var value)||value==Guid.Empty)throw new UserInputException("El identificador de entidad no es válido.");id=value;}
        Rows(await Api.AuditAsync(actor,Action,EntityType,id,DateOnly.FromDateTime(DateFrom),DateOnly.FromDateTime(DateTo),Page),x=>new(x.Action+" · "+x.EntityType,$"{MobileDates.Display(x.CreatedAt)}\nActor: {x.ActorUserId}\nEntidad: {x.EntityId}",x));
    }
    protected override Task OpenAsync(AdminRow row)=>Navigation.MessageAsync("Detalle de auditoría",row.Summary+"\nAntes:\n"+AdminPresentation.AuditValues(((AuditResponse)row.Value).OldValues)+"\nDespués:\n"+AdminPresentation.AuditValues(((AuditResponse)row.Value).NewValues)+"\nTraceId: "+((AuditResponse)row.Value).TraceId);
}

