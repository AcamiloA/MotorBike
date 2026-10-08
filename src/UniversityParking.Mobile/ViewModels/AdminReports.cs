using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using UniversityParking.Mobile.Core;
using UniversityParking.Contracts.Reporting;

namespace UniversityParking.Mobile.ViewModels;

public partial class AdminReportsViewModel(AdminApiService api,IAuthSession session):PagedUserViewModel<AdminRow>
{
    public IReadOnlyList<TypeChoice> Reports {get;}=[new("DAILY","Accesos diarios"),new("VEHICLE_TYPE","Por tipo de vehículo"),new("MEMBER_TYPE","Por tipo de miembro"),new("VEHICLE_HISTORY","Historial de vehículo"),new("USER_HISTORY","Historial de usuario"),new("GUARD_ACTIVITY","Actividad de celador")];
    [ObservableProperty] private TypeChoice selectedReport=new("DAILY","Accesos diarios");
    [ObservableProperty] private DateTime dateFrom=MobileDates.Today.AddDays(-30);
    [ObservableProperty] private DateTime dateTo=MobileDates.Today;
    [ObservableProperty] private string identification="";
    [ObservableProperty] private string identifier="";
    [ObservableProperty] private LotOption selectedLot=new(null,"Todos los parqueaderos");
    public ObservableCollection<LotOption> Lots {get;}=[];
    public Guid? TargetUserId {get;set;}
    public Guid? TargetVehicleId {get;set;}
    public bool NeedsVehicle=>SelectedReport?.Code=="VEHICLE_HISTORY";
    public bool NeedsUser=>SelectedReport?.Code is "USER_HISTORY" or "GUARD_ACTIVITY";
    public bool UsesDates=>SelectedReport?.Code is not("VEHICLE_HISTORY" or "USER_HISTORY");
    partial void OnSelectedReportChanged(TypeChoice value){Page=1;Items.Clear();TotalPages=0;TargetUserId=null;TargetVehicleId=null;foreach(var name in new[]{nameof(NeedsVehicle),nameof(NeedsUser),nameof(UsesDates)})OnPropertyChanged(name);}
    partial void OnIdentificationChanged(string value)=>TargetUserId=null;
    partial void OnIdentifierChanged(string value)=>TargetVehicleId=null;
    [RelayCommand] private Task InitializeAsync()=>WorkAsync(async()=>
    {
        AdminPresentation.RequireAdmin(session);if(Lots.Count>0)return;var list=new List<LotOption>{new(null,"Todos los parqueaderos")};for(var page=1;;page++){var result=await api.LotsAsync(null,page);if(!Accepted(result))return;list.AddRange(result.Value!.Items.Select(x=>new LotOption(x.Id,x.Name)));if(page>=result.Value.TotalPages)break;}foreach(var value in list)Lots.Add(value);SelectedLot=Lots[0];
    });
    public Task LoadAsync()=>LoadPageAsync();
    protected override Task LoadPageAsync()=>WorkAsync(async()=>
    {
        AdminPresentation.RequireAdmin(session);if(UsesDates&&DateTo.Date<DateFrom.Date)throw new UserInputException("La fecha final debe ser mayor o igual a la inicial.");
        var from=DateOnly.FromDateTime(DateFrom);var to=DateOnly.FromDateTime(DateTo);
        if(NeedsVehicle&&TargetVehicleId is null){var found=await api.FindVehicleAsync(Identifier);if(!Accepted(found))return;TargetVehicleId=found.Value!.Id;}
        if(NeedsUser&&TargetUserId is null){var found=await api.FindUserAsync(Identification);if(!Accepted(found))return;if(SelectedReport?.Code=="GUARD_ACTIVITY"&&!found.Value!.Roles.Contains("GUARD"))throw new UserInputException("Selecciona un usuario con rol GUARD.");TargetUserId=found.Value!.Id;}
        switch(SelectedReport?.Code)
        {
            case "DAILY":var daily=await api.DailyAsync(from,to,SelectedLot?.Id);if(Accepted(daily))Local(daily.Value!.Select(x=>new AdminRow(x.Date.ToString("dd/MM/yyyy"),$"Entradas: {x.CheckIns}\nSalidas: {x.CheckOuts}",x)));break;
            case "VEHICLE_TYPE":case "MEMBER_TYPE":var groups=await api.GroupAsync(SelectedReport?.Code=="VEHICLE_TYPE",from,to,SelectedLot?.Id);if(Accepted(groups))Local(groups.Value!.Select(x=>new AdminRow(SelectedReport?.Code=="VEHICLE_TYPE"?VehiclePresentation.TypeName(x.Type):AdminPresentation.Member(x.Type),$"Entradas: {x.CheckIns}\nSalidas: {x.CheckOuts}",x)));break;
            case "VEHICLE_HISTORY":var vehicle=await api.VehicleHistoryAsync(TargetVehicleId!.Value,Page);if(Accepted(vehicle))VehicleRows(vehicle.Value!);break;
            case "USER_HISTORY":var user=await api.UserHistoryAsync(TargetUserId!.Value,Page);if(Accepted(user))UserRows(user.Value!);break;
            case "GUARD_ACTIVITY":var guard=await api.GuardActivityAsync(TargetUserId!.Value,from,to);if(Accepted(guard))Local([new AdminRow("Actividad de celador",$"Entradas: {guard.Value!.CheckIns}\nSalidas: {guard.Value.CheckOuts}\nIncidentes reportados: {guard.Value.IncidentsReported}",guard.Value)]);break;
            default:throw new UserInputException("Selecciona un reporte válido.");
        }
    });
    private void Local(IEnumerable<AdminRow> values){var rows=values.ToArray();var pages=(rows.Length+19)/20;Page=Math.Min(Page,Math.Max(1,pages));Apply(new(rows.Skip((Page-1)*20).Take(20).ToArray(),Page,20,rows.Length,pages));}
    private static AdminRow Ownership(OwnershipHistoryResponse x)=>new("Propiedad",$"Usuario: {x.UserId}\nDesde: {MobileDates.Display(x.StartAt)}\nHasta: {(x.EndAt is null?"Vigente":MobileDates.Display(x.EndAt.Value))}\nMotivo: {x.TransferReason}",x);
    private static AdminRow Registration(RegistrationHistoryResponse x)=>new("Registro académico",$"Usuario: {x.UserId}\nPeriodo: {x.AcademicPeriodId}\nEstado: {x.Status}\nRegistrado: {MobileDates.Display(x.RegisteredAt)}\nCancelado: {(x.CancelledAt is null?"—":MobileDates.Display(x.CancelledAt.Value))}",x);
    private static AdminRow Movement(MovementHistoryResponse x)=>new("Movimiento",$"Vehículo: {x.VehicleId}\nParqueadero: {x.ParkingLotId}\nEntrada: {MobileDates.Display(x.CheckInAt)}\nSalida: {(x.CheckOutAt is null?"Pendiente":MobileDates.Display(x.CheckOutAt.Value))}\nEstado: {x.Status}",x);
    private static AdminRow Incident(IncidentHistoryResponse x)=>new("Incidente",$"{GuardPresentation.IncidentType(x.Type)} · {GuardPresentation.IncidentStatus(x.Status)}\n{MobileDates.Display(x.OccurredAt)}\n{x.Description}",x);
    private void VehicleRows(VehicleHistoryResponse value)
    {
        var rows=value.Ownerships.Items.Select(Ownership).Concat(value.Registrations.Items.Select(Registration)).Concat(value.Movements.Items.Select(Movement)).Concat(value.Incidents.Items.Select(Incident)).ToArray();
        Apply(new(rows,Page,20,value.Ownerships.TotalCount+value.Registrations.TotalCount+value.Movements.TotalCount+value.Incidents.TotalCount,new[]{value.Ownerships.TotalPages,value.Registrations.TotalPages,value.Movements.TotalPages,value.Incidents.TotalPages}.Max()));
    }
    private void UserRows(UserHistoryResponse value)
    {var rows=value.Ownerships.Items.Select(Ownership).Concat(value.Movements.Items.Select(Movement)).Concat(value.Incidents.Items.Select(Incident)).ToArray();Apply(new(rows,Page,20,value.Ownerships.TotalCount+value.Movements.TotalCount+value.Incidents.TotalCount,new[]{value.Ownerships.TotalPages,value.Movements.TotalPages,value.Incidents.TotalPages}.Max()));}
}
public partial class AdminIncidentCreateViewModel(AdminApiService api,GuardApiService uploads,IAuthSession session,IAttachmentPicker picker,IUserNavigation navigation):AdminFeatureViewModel(session)
{
    public ObservableCollection<LotOption> Lots {get;}=[];
    public ObservableCollection<TypeChoice> Types {get;}=new(GuardPresentation.IncidentTypes);
    public ObservableCollection<PickedAttachment> Attachments {get;}=[];
    [ObservableProperty] private LotOption? selectedLot;
    [ObservableProperty] private TypeChoice selectedType=GuardPresentation.IncidentTypes[0];
    [ObservableProperty] private string description="";
    [ObservableProperty] private string identification="";
    [ObservableProperty] private string identifier="";
    [ObservableProperty] private bool hasOccurredAt;
    [ObservableProperty] private DateTime occurredDate=MobileDates.Today;
    [ObservableProperty] private TimeSpan occurredTime=TimeSpan.FromHours(12);
    public IncidentContext? Context {get;set;}
    [RelayCommand] private Task LoadAsync()=>WorkAsync(async()=>
    {Require();if(Lots.Count>0)return;for(var page=1;;page++){var result=await api.LotsAsync(null,page);if(!Accepted(result))return;foreach(var lot in result.Value!.Items)Lots.Add(new(lot.Id,lot.Name));if(page>=result.Value.TotalPages)break;}SelectedLot=Context is null?(Lots.Count==1?Lots[0]:null):Lots.FirstOrDefault(x=>x.Id==Context.ParkingLotId);});
    [RelayCommand] private Task PickAsync()=>WorkAsync(async()=>{if(Completed||WriteUncertain)return;Require();if(Attachments.Count>=4)throw new UserInputException("Puedes adjuntar hasta cuatro archivos de 10 MB.");var file=await picker.DocumentAsync();if(file is not null)Attachments.Add(AttachmentValidation.Validate(file.FileName,file.ContentType,file.Bytes,false));});
    [RelayCommand] private void Remove(PickedAttachment? file){if(CanSave&&file is not null)Attachments.Remove(file);}
    [RelayCommand] private Task SaveAsync()=>WorkAsync(async()=>
    {
        if(Completed||WriteUncertain)return;Require();Required(Description,"La descripción",int.MaxValue);if(SelectedLot?.Id is not{} lot)throw new UserInputException("Selecciona un parqueadero.");
        if(SelectedType is null||!Types.Any(x=>x.Code==SelectedType.Code))throw new UserInputException("Selecciona un tipo de incidente válido.");
        Guid? user=Context?.UserId,vehicle=Context?.VehicleId;
        if(Context is null&&!string.IsNullOrWhiteSpace(Identification)){var found=await api.FindUserAsync(Identification);if(!Accepted(found))return;user=found.Value!.Id;}
        if(Context is null&&!string.IsNullOrWhiteSpace(Identifier)){var found=await api.FindVehicleAsync(Identifier);if(!Accepted(found))return;vehicle=found.Value!.Id;}
        if(Context?.MovementId is not null&&Context.ParkingLotId!=lot)throw new UserInputException("El parqueadero debe corresponder al movimiento seleccionado.");
        var input=new IncidentInput(new(lot,user,vehicle,Context?.MovementId),SelectedType.Code,Description,HasOccurredAt?GuardPresentation.BogotaInstant(OccurredDate,OccurredTime):null,Attachments.ToArray());
        var result=await uploads.CreateIncidentAsync(input);if(Mutation(result)){Completed=true;await navigation.GoAsync("admin-incident-detail",new Dictionary<string,object>{["incidentId"]=result.Value!.Id});}
    });
    [RelayCommand] private Task ListAsync()=>navigation.GoAsync("admin-incidents");
}

