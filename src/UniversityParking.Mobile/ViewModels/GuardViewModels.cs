using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using UniversityParking.Mobile.Core;
using UniversityParking.Contracts.Common;
using UniversityParking.Contracts.Parking;
using UniversityParking.Contracts.ParkingLots;
using UniversityParking.Contracts.Reporting;
using UniversityParking.Contracts.Incidents;

namespace UniversityParking.Mobile.ViewModels;

public partial class GuardLotSession(GuardApiService api, IAuthSession session, IGuardSelectionStore store) : ObservableObject
{
    public ObservableCollection<ParkingLotResponse> Lots { get; } = [];
    [ObservableProperty] private ParkingLotResponse? selected;
    private Guid? owner;
    partial void OnSelectedChanged(ParkingLotResponse? value) { if (session.User is { } user) store.Set(user.Id, value?.Id); }
    public bool IsGuard => session.User?.Roles.Contains("GUARD") == true;
    public void RequireGuard() { if (!IsGuard) throw new UserInputException("Se requiere el rol GUARD para operar en portería."); }
    public Guid RequireLot() { RequireGuard(); return Selected?.Id ?? throw new UserInputException("Selecciona un parqueadero activo."); }
    public async Task RefreshAsync()
    {
        RequireGuard(); var user = session.User!; var token = await session.GetTokenAsync();
        var previous = owner == user.Id ? Selected?.Id ?? store.Get(user.Id) : store.Get(user.Id);
        var all = new List<ParkingLotResponse>();
        for (var page = 1; ; page++)
        {
            var result = await api.LotsAsync(page);
            if (!result.IsSuccess) { Selected = null; Lots.Clear(); throw new UserInputException(result.Error!.Message); }
            all.AddRange(result.Value!.Items.Where(x => x.Status == "ACTIVE")); if (page >= result.Value.TotalPages) break;
        }
        if (session.User?.Id != user.Id || await session.GetTokenAsync() != token) return;
        owner = user.Id; Lots.Clear(); foreach (var lot in all) Lots.Add(lot);
        Selected = all.Count == 1 ? all[0] : all.FirstOrDefault(x => x.Id == previous);
    }
}
public partial class GuardHomeViewModel(GuardApiService api, GuardLotSession lots, IUserNavigation navigation, AuthService auth) : UserFeatureViewModel
{
    public GuardLotSession Lots => lots;
    [ObservableProperty] private GuardDashboardResponse? dashboard;
    public string Summary => Dashboard is null ? "Selecciona un parqueadero para consultar el estado." : $"Dentro: {Dashboard.VehiclesInside}\nEntradas hoy: {Dashboard.TodayCheckIns}\nSalidas hoy: {Dashboard.TodayCheckOuts}\nIncidentes abiertos: {Dashboard.OpenIncidents}";
    partial void OnDashboardChanged(GuardDashboardResponse? value) => OnPropertyChanged(nameof(Summary));
    [RelayCommand] private Task LoadAsync() => WorkAsync(async () => { Dashboard = null; await lots.RefreshAsync(); if (lots.Selected is not null) await DashboardAsync(); else if (lots.Lots.Count == 0) ErrorMessage = "No hay parqueaderos activos."; });
    [RelayCommand] private Task UpdateAsync() => WorkAsync(DashboardAsync);
    private async Task DashboardAsync() { Dashboard = null; var result = await api.DashboardAsync(lots.RequireLot()); if (Accepted(result)) Dashboard = result.Value; }
    [RelayCommand] private Task ScanAsync() => navigation.GoAsync("guard-access-control");
    [RelayCommand] private Task ManualAsync() => navigation.GoAsync("guard-access-control");
    [RelayCommand] private Task InsideAsync() => navigation.GoAsync("guard-inside");
    [RelayCommand] private Task IncidentsAsync() => navigation.GoAsync("guard-incidents");
    [RelayCommand] private Task HistoryAsync() => navigation.GoAsync("guard-history");
    [RelayCommand] private Task CreateIncidentAsync() => WorkAsync(() => navigation.GoAsync("guard-create-incident", new Dictionary<string, object> { ["context"] = new IncidentContext(lots.RequireLot()) }));
    [RelayCommand] private Task LogoutAsync() => WorkAsync(auth.LogoutAsync);
}
public sealed record GuardMovementCard(ParkingMovementResponse Movement)
{
    public string Heading => $"{Movement.UserFullName} · {Movement.VehicleIdentifier}";
    public string Summary => $"{VehiclePresentation.TypeName(Movement.VehicleType)} · {Movement.ParkingLotName}";
    public string Entry => "Entrada: " + MobileDates.Display(Movement.CheckInAtUtc);
    public string Exit => Movement.CheckOutAtUtc is null ? "Salida pendiente" : "Salida: " + MobileDates.Display(Movement.CheckOutAtUtc.Value);
    public string Status => Movement.Status == "OPEN" ? "Dentro" : "Salida registrada";
}
public partial class VehiclesInsideViewModel(GuardApiService api, GuardLotSession lots, IUserNavigation navigation) : PagedUserViewModel<GuardMovementCard>
{
    public GuardLotSession Lots => lots;
    public IReadOnlyList<TypeChoice> Types => GuardPresentation.VehicleTypes;
    [ObservableProperty] private TypeChoice selectedType = GuardPresentation.VehicleTypes[0];
    [ObservableProperty] private string search = "";
    [ObservableProperty] private string counts = "";
    public Task LoadAsync() => LoadPageAsync();
    protected override Task LoadPageAsync() => WorkAsync(async () =>
    {
        Counts = ""; lots.RequireGuard(); await lots.RefreshAsync(); var result = await api.InsideAsync(lots.RequireLot(), SelectedType.Code, Search, Page); if (!Accepted(result)) return;
        var value = result.Value!; Apply(new(value.Items.Select(x => new GuardMovementCard(x)).ToArray(), value.Page, value.PageSize, value.TotalCount, value.TotalPages));
        Counts = $"Total: {value.Counts.Total} · Carros: {value.Counts.Cars} · Motos: {value.Counts.Motorcycles} · Bicicletas: {value.Counts.Bicycles}";
    });
    [RelayCommand] private Task ExitAsync(GuardMovementCard? item) => item is null ? Task.CompletedTask : navigation.GoAsync("guard-access-control", new Dictionary<string, object> { ["movement"] = item.Movement });
    [RelayCommand] private Task IncidentAsync(GuardMovementCard? item) => item is null ? Task.CompletedTask : navigation.GoAsync("guard-create-incident", new Dictionary<string, object> { ["context"] = new IncidentContext(item.Movement.ParkingLotId, item.Movement.UserId, item.Movement.VehicleId, item.Movement.MovementId, item.Movement.UserFullName, item.Movement.VehicleIdentifier, item.Movement.ParkingLotName) });
}
public partial class ParkingHistoryViewModel(GuardApiService api, GuardLotSession lots) : PagedUserViewModel<GuardMovementCard>
{
    public GuardLotSession Lots => lots;
    public IReadOnlyList<TypeChoice> Types => GuardPresentation.VehicleTypes;
    public IReadOnlyList<TypeChoice> States { get; } = [new("", "Todos los estados"), new("OPEN", "Dentro"), new("CLOSED", "Salida registrada")];
    [ObservableProperty] private TypeChoice selectedType = GuardPresentation.VehicleTypes[0];
    [ObservableProperty] private TypeChoice selectedState = new("", "Todos los estados");
    [ObservableProperty] private DateTime dateFrom = MobileDates.Today.AddDays(-30);
    [ObservableProperty] private DateTime dateTo = MobileDates.Today;
    [ObservableProperty] private string identification = "";
    [ObservableProperty] private string plate = "";
    [ObservableProperty] private string frame = "";
    public Task LoadAsync() => LoadPageAsync();
    protected override Task LoadPageAsync() => WorkAsync(async () =>
    {
        lots.RequireGuard(); if (DateTo.Date < DateFrom.Date) throw new UserInputException("La fecha final debe ser mayor o igual a la inicial."); await lots.RefreshAsync();
        var result = await api.HistoryAsync(lots.RequireLot(), DateOnly.FromDateTime(DateFrom), DateOnly.FromDateTime(DateTo), Identification, Plate, Frame, SelectedType.Code, SelectedState.Code, Page);
        if (Accepted(result)) { var value = result.Value!; Apply(new(value.Items.Select(x => new GuardMovementCard(x)).ToArray(), value.Page, value.PageSize, value.TotalCount, value.TotalPages)); }
    });
}
public sealed record IncidentCard(IncidentResponse Incident)
{
    public string Heading => GuardPresentation.IncidentType(Incident.Type) + " · " + GuardPresentation.IncidentStatus(Incident.Status);
    public string Description => Incident.Description;
    public string Date => MobileDates.Display(Incident.OccurredAt);
}
public partial class IncidentsViewModel(GuardApiService api, GuardLotSession lots, IUserNavigation navigation) : PagedUserViewModel<IncidentCard>
{
    public GuardLotSession Lots => lots;
    public IReadOnlyList<TypeChoice> Types { get; } = new[] { new TypeChoice("", "Todos los tipos") }.Concat(GuardPresentation.IncidentTypes).ToArray();
    public IReadOnlyList<TypeChoice> States { get; } = [new("", "Todos los estados"), new("OPEN", "Abierto"), new("RESOLVED", "Resuelto"), new("CANCELLED", "Cancelado")];
    [ObservableProperty] private TypeChoice selectedType = new("", "Todos los tipos");
    [ObservableProperty] private TypeChoice selectedState = new("", "Todos los estados");
    [ObservableProperty] private DateTime dateFrom = MobileDates.Today.AddDays(-30);
    [ObservableProperty] private DateTime dateTo = MobileDates.Today;
    public Task LoadAsync() => LoadPageAsync();
    protected override Task LoadPageAsync() => WorkAsync(async () =>
    {
        lots.RequireGuard(); if (DateTo.Date < DateFrom.Date) throw new UserInputException("La fecha final debe ser mayor o igual a la inicial."); await lots.RefreshAsync();
        var result = await api.IncidentsAsync(lots.RequireLot(), SelectedType.Code, SelectedState.Code, DateOnly.FromDateTime(DateFrom), DateOnly.FromDateTime(DateTo), Page);
        if (Accepted(result)) { var value = result.Value!; Apply(new(value.Items.Select(x => new IncidentCard(x)).ToArray(), value.Page, value.PageSize, value.TotalCount, value.TotalPages)); }
    });
    [RelayCommand] private Task OpenAsync(IncidentCard? item) => item is null ? Task.CompletedTask : navigation.GoAsync("guard-incident-detail", new Dictionary<string, object> { ["incidentId"] = item.Incident.Id });
    [RelayCommand] private Task CreateAsync() => WorkAsync(() => navigation.GoAsync("guard-create-incident", new Dictionary<string, object> { ["context"] = new IncidentContext(lots.RequireLot()) }));
}
public partial class CreateIncidentViewModel(GuardApiService api, GuardLotSession lots, IAttachmentPicker picker, IUserNavigation navigation) : UserFeatureViewModel
{
    public GuardLotSession Lots => lots;
    public IReadOnlyList<TypeChoice> Types => GuardPresentation.IncidentTypes;
    [ObservableProperty] private IncidentContext? context;
    [ObservableProperty] private TypeChoice selectedType = GuardPresentation.IncidentTypes[0];
    [ObservableProperty] private string description = "";
    [ObservableProperty] private string userId = "";
    [ObservableProperty] private string vehicleId = "";
    [ObservableProperty] private string movementId = "";
    [ObservableProperty] private string identification = "";
    [ObservableProperty] private string associationLabel = "Sin usuario asociado";
    [ObservableProperty] private EligibleVehicleResponse? relatedVehicle;
    public ObservableCollection<EligibleVehicleResponse> RelatedVehicles { get; } = [];
    public bool CanAssociate => Context?.MovementId is null && !Completed && !IsBusy;
    public bool CanSave => !IsBusy && !Completed;
    protected override void OnPropertyChanged(System.ComponentModel.PropertyChangedEventArgs e) { base.OnPropertyChanged(e); if (e.PropertyName is nameof(IsBusy) or nameof(Context) or nameof(Completed)) { base.OnPropertyChanged(new System.ComponentModel.PropertyChangedEventArgs(nameof(CanAssociate))); base.OnPropertyChanged(new System.ComponentModel.PropertyChangedEventArgs(nameof(CanSave))); } }
    partial void OnRelatedVehicleChanged(EligibleVehicleResponse? value) { if (Context?.MovementId is null) VehicleId = value?.Id.ToString() ?? ""; }
    [ObservableProperty] private bool hasOccurredAt;
    [ObservableProperty] private DateTime occurredDate = MobileDates.Today;
    [ObservableProperty] private TimeSpan occurredTime = TimeSpan.FromHours(12);
    [ObservableProperty] private bool completed;
    public ObservableCollection<PickedAttachment> Attachments { get; } = [];
    public string ContextLabel => Context?.MovementId is null ? "Asociación de usuario y vehículo opcional" : $"Incidente del movimiento seleccionado\n{Context.UserName}\n{Context.VehicleIdentifier} · {Context.ParkingLotName}";
    partial void OnContextChanged(IncidentContext? value) { UserId = value?.UserId?.ToString() ?? ""; VehicleId = value?.VehicleId?.ToString() ?? ""; MovementId = value?.MovementId?.ToString() ?? ""; AssociationLabel = value?.UserName ?? "Sin usuario asociado"; OnPropertyChanged(nameof(ContextLabel)); }
    [RelayCommand] private Task AssociateAsync() => WorkAsync(async () =>
    {
        if (Context?.MovementId is not null || Completed) return; lots.RequireGuard(); Required(Identification, "La identificación", 50);
        var result = await api.LookupAsync(new(null, Identification.Trim())); if (!Accepted(result)) return;
        UserId = result.Value!.User.Id.ToString(); AssociationLabel = result.Value.User.FullName; MovementId = ""; RelatedVehicle = null; VehicleId = ""; RelatedVehicles.Clear();
        foreach (var vehicle in result.Value.EligibleVehicles) RelatedVehicles.Add(vehicle);
        if (result.Value.CurrentMovement is { } current)
        { lots.Selected = lots.Lots.FirstOrDefault(x => x.Id == current.ParkingLotId); Context = new(current.ParkingLotId,current.UserId,current.VehicleId,current.MovementId,current.UserFullName,current.VehicleIdentifier,current.ParkingLotName); }
        else if (RelatedVehicles.Count == 1) RelatedVehicle = RelatedVehicles[0];
    });
    [RelayCommand] private void ClearAssociation() { if (!CanAssociate) return; UserId = VehicleId = MovementId = ""; RelatedVehicle = null; RelatedVehicles.Clear(); AssociationLabel = "Sin usuario asociado"; }
    [RelayCommand] private Task LoadAsync() => WorkAsync(async () => { await lots.RefreshAsync(); if (Context is not null) lots.Selected = lots.Lots.FirstOrDefault(x => x.Id == Context.ParkingLotId); });
    [RelayCommand] private Task PickAsync() => WorkAsync(async () =>
    { if (Completed) return; if (Attachments.Count >= 4) throw new UserInputException("Puedes adjuntar hasta cuatro archivos de 10 MB."); var file = await picker.DocumentAsync(); if (file is not null) Attachments.Add(AttachmentValidation.Validate(file.FileName, file.ContentType, file.Bytes, false)); });
    [RelayCommand] private void Remove(PickedAttachment? file) { if (!IsBusy && !Completed && file is not null) Attachments.Remove(file); }
    private static Guid? OptionalId(string text, string name) => string.IsNullOrWhiteSpace(text) ? null : Guid.TryParse(text.Trim(), out var id) && id != Guid.Empty ? id : throw new UserInputException($"{name} debe ser un identificador UUID válido.");
    [RelayCommand] private Task SaveAsync() => WorkAsync(async () =>
    {
        if (Completed) return; lots.RequireGuard(); Required(Description, "La descripción", 4000);
        if (!Types.Any(x => x.Code == SelectedType.Code)) throw new UserInputException("Selecciona un tipo de incidente válido.");
        if (Context?.MovementId is not null && lots.RequireLot() != Context.ParkingLotId) throw new UserInputException("El parqueadero debe corresponder al movimiento seleccionado.");
        var occurrence = HasOccurredAt ? GuardPresentation.BogotaInstant(OccurredDate, OccurredTime) : (DateTimeOffset?)null;
        var input = new IncidentInput(new(lots.RequireLot(), OptionalId(UserId, "Usuario"), OptionalId(VehicleId, "Vehículo"), OptionalId(MovementId, "Movimiento")), SelectedType.Code, Description, occurrence, Attachments.ToArray());
        var result = await api.CreateIncidentAsync(input);
        if (Accepted(result)) { Completed = true; await navigation.MessageAsync("Incidente", "Incidente registrado correctamente."); await navigation.GoAsync("guard-incident-detail", new Dictionary<string, object> { ["incidentId"] = result.Value!.Id }); }
        else if (GuardPresentation.Uncertain(result.Error)) { Completed = true; ErrorMessage = "No se pudo confirmar el resultado. Consulta la lista de incidentes antes de registrar otro."; }
    });
    [RelayCommand] private Task ListAsync() => navigation.GoAsync("guard-incidents");
}
public partial class IncidentDetailViewModel(GuardApiService api, GuardLotSession lots, IFileViewer viewer, IAuthSession session) : UserFeatureViewModel
{
    public Guid IncidentId { get; set; }
    [ObservableProperty] private IncidentDetailResponse? detail;
    public string Summary => Detail is null ? "" : $"{GuardPresentation.IncidentType(Detail.Incident.Type)} · {GuardPresentation.IncidentStatus(Detail.Incident.Status)}\n{Detail.Incident.Description}\nOcurrido: {MobileDates.Display(Detail.Incident.OccurredAt)}\nReportado por: {Detail.Incident.ReportedBy}\nParqueadero: {Detail.Incident.ParkingLotId}\nUsuario: {Detail.Incident.UserId}\nVehículo: {Detail.Incident.VehicleId}\nMovimiento: {Detail.Incident.ParkingMovementId}\nResolución: {Detail.Incident.Resolution ?? "Pendiente"}";
    partial void OnDetailChanged(IncidentDetailResponse? value) => OnPropertyChanged(nameof(Summary));
    [RelayCommand] private Task LoadAsync() => WorkAsync(async () => { lots.RequireGuard(); Detail = null; var result = await api.IncidentAsync(IncidentId); if (Accepted(result)) Detail = result.Value; });
    [RelayCommand] private Task OpenAsync(IncidentAttachmentResponse? attachment) => WorkAsync(async () =>
    { if (attachment is null) return; lots.RequireGuard(); var token = await session.GetTokenAsync(); var result = await api.FileAsync(attachment.ContentUrl); if (Accepted(result) && session.User is not null && token == await session.GetTokenAsync()) await viewer.OpenAsync(attachment.OriginalFileName, attachment.ContentType, result.Value!); });
}
