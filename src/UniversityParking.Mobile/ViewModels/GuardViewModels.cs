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
    [RelayCommand] private Task ScanAsync() => navigation.GoAsync("guard-scan");
    [RelayCommand] private Task ManualAsync() => navigation.GoAsync("guard-manual");
    [RelayCommand] private Task InsideAsync() => navigation.GoAsync("guard-inside");
    [RelayCommand] private Task IncidentsAsync() => navigation.GoAsync("guard-incidents");
    [RelayCommand] private Task HistoryAsync() => navigation.GoAsync("guard-history");
    [RelayCommand] private Task CreateIncidentAsync() => WorkAsync(() => navigation.GoAsync("guard-create-incident", new Dictionary<string, object> { ["context"] = new IncidentContext(lots.RequireLot()) }));
    [RelayCommand] private Task LogoutAsync() => WorkAsync(auth.LogoutAsync);
}
public partial class GuardLookupViewModel(GuardApiService api, GuardLotSession lots, IUserNavigation navigation, IScannerPermission permission) : UserFeatureViewModel
{
    [ObservableProperty] private string identificationNumber = "";
    [ObservableProperty] private bool isScanning;
    private int scanClaimed; private int generation;
    public void Stop() { generation++; IsScanning = false; Interlocked.Exchange(ref scanClaimed, 1); }
    [RelayCommand] private Task StartAsync() => WorkAsync(async () =>
    {
        Stop(); var epoch = generation; lots.RequireGuard();
        if (!await permission.RequestAsync()) { ErrorMessage = "No se concedió permiso para usar la cámara. Usa la búsqueda manual por identificación."; return; }
        if (epoch != generation) return; Interlocked.Exchange(ref scanClaimed, 0); IsScanning = true;
    });
    public async Task ScanAsync(string? code)
    {
        if (!IsScanning || string.IsNullOrWhiteSpace(code) || Interlocked.CompareExchange(ref scanClaimed, 1, 0) != 0) return;
        IsScanning = false; await LookupAsync(new(code, null));
    }
    [RelayCommand] private Task SearchAsync() => WorkAsync(async () =>
    { lots.RequireGuard(); Required(IdentificationNumber, "La identificación", 50); await LookupCoreAsync(new(null, IdentificationNumber.Trim())); });
    private Task LookupAsync(ParkingAccessRequest request) => WorkAsync(async () => { lots.RequireGuard(); Required(request.CardCode!, "El código de carné", 100); await LookupCoreAsync(request); });
    private async Task LookupCoreAsync(ParkingAccessRequest request)
    {
        var epoch = generation; var result = await api.LookupAsync(request);
        if (Accepted(result) && epoch == generation && lots.IsGuard) await navigation.GoAsync("guard-access", new Dictionary<string, object> { ["access"] = new AccessContext(request, result.Value!) });
    }
    [RelayCommand] private Task ManualAsync() { Stop(); return navigation.GoAsync("guard-manual"); }
}
public partial class AccessResultViewModel(GuardLotSession lots, IUserNavigation navigation) : UserFeatureViewModel
{
    [ObservableProperty] private AccessContext? access;
    [ObservableProperty] private EligibleVehicleResponse? selectedVehicle;
    public ObservableCollection<EligibleVehicleResponse> Vehicles { get; } = [];
    public string UserSummary => Access is null ? "" : $"{Access.Result.User.FullName}\n{Access.Result.User.MemberType}\n{AdminPresentation.Status(Access.Result.User.Status)}";
    public string MovementSummary => Access?.Result.CurrentMovement is { } movement ? $"Vehículo dentro: {movement.VehicleIdentifier}\n{movement.ParkingLotName}\nIngreso: {MobileDates.Display(movement.CheckInAtUtc)}" : "No hay vehículo dentro.";
    public bool CanEnter => Access is { Result.User.Status: "ACTIVE", Result.CurrentMovement: null } && SelectedVehicle is not null && lots.IsGuard;
    public bool CanExit => Access?.Result.CurrentMovement is not null && lots.IsGuard;
    public bool ShowVehicles => Access is { Result.User.Status: "ACTIVE", Result.CurrentMovement: null };
    public string EmptyMessage => Access?.Result.CurrentMovement is null && Vehicles.Count == 0 ? "No hay vehículos habilitados para ingreso." : "";
    partial void OnAccessChanged(AccessContext? value)
    {
        Vehicles.Clear(); foreach (var vehicle in value?.Result.EligibleVehicles ?? []) Vehicles.Add(vehicle); SelectedVehicle = Vehicles.Count == 1 ? Vehicles[0] : null;
        foreach (var property in new[] { nameof(UserSummary), nameof(MovementSummary), nameof(CanEnter), nameof(CanExit), nameof(EmptyMessage), nameof(ShowVehicles) }) OnPropertyChanged(property);
    }
    partial void OnSelectedVehicleChanged(EligibleVehicleResponse? value) => OnPropertyChanged(nameof(CanEnter));
    [RelayCommand] private Task EnterAsync() => WorkAsync(async () => { lots.RequireGuard(); if (!CanEnter) throw new UserInputException("Selecciona un vehículo habilitado de un usuario activo."); await navigation.GoAsync("guard-check-in", new Dictionary<string, object> { ["entry"] = new EntryContext(Access!, SelectedVehicle!) }); });
    [RelayCommand] private Task ExitAsync() => WorkAsync(async () => { lots.RequireGuard(); if (!CanExit) return; await navigation.GoAsync("guard-check-out", new Dictionary<string, object> { ["movement"] = Access!.Result.CurrentMovement! }); });
}
public partial class CheckInViewModel(GuardApiService api, GuardLotSession lots, IUserNavigation navigation) : UserFeatureViewModel
{
    [ObservableProperty] private EntryContext? entry;
    [ObservableProperty] private bool completed;
    [ObservableProperty] private bool needsVerification;
    [ObservableProperty] private string resultMessage = "";
    private Guid? attemptedLot; private int verificationGeneration;
    public GuardLotSession Lots => lots;
    public bool CanSubmit => !IsBusy && !Completed && !NeedsVerification && Entry is not null;
    protected override void OnPropertyChanged(System.ComponentModel.PropertyChangedEventArgs e) { base.OnPropertyChanged(e); if (e.PropertyName is nameof(IsBusy) or nameof(Entry)) base.OnPropertyChanged(new System.ComponentModel.PropertyChangedEventArgs(nameof(CanSubmit))); }
    public string Summary => Entry is null ? "" : $"{Entry.Access.Result.User.FullName} · {Entry.Access.Result.User.MemberType}\n{VehiclePresentation.TypeName(Entry.Vehicle.Type)} · {Entry.Vehicle.Identifier}\n{Entry.Vehicle.Brand} {Entry.Vehicle.Model}\nHora actual: {MobileDates.Display(DateTimeOffset.UtcNow)}";
    partial void OnEntryChanged(EntryContext? value) => OnPropertyChanged(nameof(Summary));
    partial void OnCompletedChanged(bool value) => OnPropertyChanged(nameof(CanSubmit));
    partial void OnNeedsVerificationChanged(bool value) => OnPropertyChanged(nameof(CanSubmit));
    [RelayCommand] private Task LoadAsync() => WorkAsync(lots.RefreshAsync);
    [RelayCommand] private Task ConfirmAsync() => WorkAsync(async () =>
    {
        if (Completed || NeedsVerification || Entry is null) return; lots.RequireGuard(); await lots.RefreshAsync(); var lot = lots.RequireLot();
        var fresh = await api.LookupAsync(Entry.Access.Request); if (!Accepted(fresh)) return;
        if (fresh.Value!.User.Status != "ACTIVE" || fresh.Value.CurrentMovement is not null || !fresh.Value.EligibleVehicles.Any(x => x.Id == Entry.Vehicle.Id))
        { ErrorMessage = "El estado cambió. Consulta nuevamente el carné antes de registrar el ingreso."; NeedsVerification = true; return; }
        if (!await navigation.ConfirmAsync("Confirmar ingreso", "¿Confirmar el ingreso de este vehículo?")) return;
        lots.RequireGuard(); attemptedLot = lot; var result = await api.CheckInAsync(fresh.Value.User.Id, Entry.Vehicle.Id, lot);
        if (result.IsSuccess) { Completed = true; ResultMessage = $"Entrada registrada correctamente.\n{result.Value!.VehicleIdentifier} · {result.Value.ParkingLotName}\n{MobileDates.Display(result.Value.CheckInAtUtc)}"; }
        else { ErrorMessage = GuardPresentation.AccessError(result.Error!); if (GuardPresentation.Uncertain(result.Error)) { NeedsVerification = true; await VerifyCoreAsync(); } }
    });
    [RelayCommand] private Task VerifyAsync() => WorkAsync(VerifyCoreAsync);
    private async Task VerifyCoreAsync()
    {
        if (Entry is null) return; lots.RequireGuard(); var sequence = ++verificationGeneration;
        var result = await api.LookupAsync(Entry.Access.Request);
        if (!Accepted(result)) { NeedsVerification = true; return; }
        if (sequence != verificationGeneration) return;
        var movement = result.Value!.CurrentMovement;
        if (movement?.VehicleId == Entry.Vehicle.Id && (attemptedLot is null || movement.ParkingLotId == attemptedLot))
        { Completed = true; NeedsVerification = false; ErrorMessage = ""; ResultMessage = $"Estado verificado: el vehículo está dentro de {movement.ParkingLotName}.\nIngreso: {MobileDates.Display(movement.CheckInAtUtc)}"; }
        else if (movement is not null || result.Value.User.Status != "ACTIVE" || !result.Value.EligibleVehicles.Any(x => x.Id == Entry.Vehicle.Id))
        { NeedsVerification = true; ErrorMessage = "El estado cambió. Finaliza y realiza una nueva búsqueda."; }
        else { NeedsVerification = false; ErrorMessage = "No hay ingreso registrado. Puedes confirmar de nuevo explícitamente."; }
    }
    [RelayCommand] private Task FinishAsync() => navigation.GoAsync("guard-home");
}
public partial class CheckOutViewModel(GuardApiService api, GuardLotSession lots, IUserNavigation navigation) : UserFeatureViewModel
{
    [ObservableProperty] private ParkingMovementResponse? movement;
    [ObservableProperty] private bool completed;
    [ObservableProperty] private bool needsVerification;
    [ObservableProperty] private string resultMessage = "";
    public bool CanSubmit => !IsBusy && !Completed && !NeedsVerification && Movement is not null;
    protected override void OnPropertyChanged(System.ComponentModel.PropertyChangedEventArgs e) { base.OnPropertyChanged(e); if (e.PropertyName is nameof(IsBusy) or nameof(Movement)) base.OnPropertyChanged(new System.ComponentModel.PropertyChangedEventArgs(nameof(CanSubmit))); }
    public string Summary => Movement is null ? "" : $"{Movement.UserFullName}\n{VehiclePresentation.TypeName(Movement.VehicleType)} · {Movement.VehicleIdentifier}\n{Movement.ParkingLotName}\nEntrada: {MobileDates.Display(Movement.CheckInAtUtc)}\nHora actual: {MobileDates.Display(DateTimeOffset.UtcNow)}\nDuración aproximada: {(DateTimeOffset.UtcNow - Movement.CheckInAtUtc).TotalMinutes:N0} minutos";
    partial void OnMovementChanged(ParkingMovementResponse? value) => OnPropertyChanged(nameof(Summary));
    partial void OnCompletedChanged(bool value) => OnPropertyChanged(nameof(CanSubmit));
    partial void OnNeedsVerificationChanged(bool value) => OnPropertyChanged(nameof(CanSubmit));
    [RelayCommand] private Task ConfirmAsync() => WorkAsync(async () =>
    {
        if (Completed || NeedsVerification || Movement is null) return; lots.RequireGuard();
        // Verify this exact movement; an old page must never close a subsequent entry of the same vehicle.
        await VerifyCoreAsync(); if (NeedsVerification || Completed) return;
        if (!await navigation.ConfirmAsync("Confirmar salida", "¿Confirmar la salida de este vehículo?")) return;
        lots.RequireGuard(); var result = await api.CheckOutAsync(Movement.VehicleId);
        if (result.IsSuccess) { Completed = true; ResultMessage = $"Salida registrada correctamente.\n{MobileDates.Display(result.Value!.CheckOutAtUtc!.Value)}\nDuración: {result.Value.Duration}"; }
        else { ErrorMessage = GuardPresentation.AccessError(result.Error!); if (GuardPresentation.Uncertain(result.Error)) { NeedsVerification = true; await VerifyCoreAsync(); } }
    });
    [RelayCommand] private Task VerifyAsync() => WorkAsync(VerifyCoreAsync);
    private async Task VerifyCoreAsync()
    {
        if (Movement is null) return; lots.RequireGuard();
        for (var page = 1; ; page++)
        {
            var result = await api.HistoryAsync(Movement.ParkingLotId, null, null, null, Movement.VehicleType == "BICYCLE" ? null : Movement.VehicleIdentifier, Movement.VehicleType == "BICYCLE" ? Movement.VehicleIdentifier : null, null, null, page);
            if (!Accepted(result)) { NeedsVerification = true; return; }
            var exact = result.Value!.Items.FirstOrDefault(x => x.MovementId == Movement.MovementId);
            if (exact is not null)
            {
                NeedsVerification = false; ErrorMessage = "";
                if (exact.Status == "CLOSED") { Completed = true; ResultMessage = $"Estado verificado: salida registrada.\n{MobileDates.Display(exact.CheckOutAtUtc!.Value)}\nDuración: {exact.Duration}"; }
                return;
            }
            if (page >= result.Value.TotalPages) { NeedsVerification = true; ErrorMessage = "No fue posible verificar el movimiento. Finaliza y consulta el estado actual."; return; }
        }
    }
    [RelayCommand] private Task FinishAsync() => navigation.GoAsync("guard-home");
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
    [RelayCommand] private Task ExitAsync(GuardMovementCard? item) => item is null ? Task.CompletedTask : navigation.GoAsync("guard-check-out", new Dictionary<string, object> { ["movement"] = item.Movement });
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


