using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using UniversityParking.Contracts.Parking;
using UniversityParking.Mobile.Core;

namespace UniversityParking.Mobile.ViewModels;

public enum GuardAccessState
{
    INITIALIZING, SCANNING, PROCESSING_IDENTITY, SELECTING_VEHICLE, CONFIRMING_ENTRY,
    CONFIRMING_EXIT, SUBMITTING, SUCCESS_ENTRY, SUCCESS_EXIT, ERROR, MANUAL_LOOKUP
}

public interface IEvidenceImageValidator { Task<bool> CanDisplayAsync(byte[] bytes); }

public sealed record GuardVehicleChoice(EligibleVehicleResponse Vehicle, byte[]? Image)
{
    public string Heading => $"{VehiclePresentation.TypeName(Vehicle.Type)} · {Vehicle.Identifier}";
    public string Description => $"{Vehicle.Brand} {Vehicle.Model}";
}

public partial class GuardAccessControlViewModel : UserFeatureViewModel
{
    private readonly GuardApiService api;
    private readonly GuardLotSession lots;
    private readonly IAuthSession session;
    private readonly IScannerPermission permission;
    private readonly IEvidenceImageValidator images;
    private readonly SynchronizationContext? uiContext = SynchronizationContext.Current;
    private bool active, cameraGranted;
    private int generation, scanClaimed;
    private string? sessionToken;
    private Guid? actorId, operationLot;
    private Guid? attemptedMovement;
    private bool attemptedExit;
    private ParkingAccessRequest? identity;
    public GuardLotSession Lots => lots;
    public ObservableCollection<GuardVehicleChoice> Vehicles { get; } = [];
    [ObservableProperty] private GuardAccessState state = GuardAccessState.INITIALIZING;
    [ObservableProperty] private string identificationNumber = "";
    [ObservableProperty] private ParkingAccessResponse? access;
    [ObservableProperty] private EligibleVehicleResponse? selectedVehicle;
    [ObservableProperty] private ParkingMovementResponse? movement;
    [ObservableProperty] private byte[]? evidence;
    [ObservableProperty] private bool evidenceRendered;
    [ObservableProperty] private bool isInspectingEvidence;
    [ObservableProperty] private bool needsVerification;
    [ObservableProperty] private string resultMessage = "";
    public bool CameraMounted => active && cameraGranted;
    public bool IsDetecting => CameraMounted && State == GuardAccessState.SCANNING && !IsBusy;
    public bool ShowManual => State == GuardAccessState.MANUAL_LOOKUP;
    public bool ShowSelection => State == GuardAccessState.SELECTING_VEHICLE;
    public bool ShowConfirmation => State is GuardAccessState.CONFIRMING_ENTRY or GuardAccessState.CONFIRMING_EXIT;
    public bool ShowEntry => State == GuardAccessState.CONFIRMING_ENTRY;
    public bool ShowExit => State == GuardAccessState.CONFIRMING_EXIT;
    public bool ShowSuccess => State is GuardAccessState.SUCCESS_ENTRY or GuardAccessState.SUCCESS_EXIT;
    public bool ShowReset => State is GuardAccessState.MANUAL_LOOKUP or GuardAccessState.SELECTING_VEHICLE or GuardAccessState.CONFIRMING_ENTRY or GuardAccessState.CONFIRMING_EXIT or GuardAccessState.ERROR;
    public bool CanConfirm => active && lots.IsGuard && !IsBusy && !NeedsVerification && !IsInspectingEvidence &&
        operationLot == lots.Selected?.Id && (ShowExit && Movement?.Status == "OPEN" || ShowEntry && EvidenceRendered && Evidence is { Length: > 0 } && SelectedVehicle is not null);
    public bool CanManual => active && lots.IsGuard && !IsBusy && !IsInspectingEvidence && !NeedsVerification;
    public bool CanReset => active && !IsBusy && !NeedsVerification;
    public string UserSummary => Access is null ? Movement?.UserFullName ?? "" : $"{Access.User.FullName}\n{Access.User.MemberType} · {AdminPresentation.Status(Access.User.Status)}";
    public string VehicleSummary => SelectedVehicle is { } vehicle ? $"{VehiclePresentation.TypeName(vehicle.Type)}\n{(vehicle.Type is "BICYCLE" or "SCOOTER" ? "MARCO REGISTRADO" : "PLACA REGISTRADA")}: {vehicle.Identifier}\n{vehicle.Brand} {vehicle.Model}" : "";
    public string MovementSummary => Movement is { } movement ? $"Ingreso: {MobileDates.Display(movement.CheckInAtUtc)}\n{movement.ParkingLotName}\nDuración aproximada: {movement.Duration.TotalMinutes:N0} minutos" : "";
    public string EvidenceLabel => SelectedVehicle?.Type is "BICYCLE" or "SCOOTER" ? VehiclePresentation.VerificationLabel(SelectedVehicle?.Type) : "Frente de la Licencia de Tránsito";
    public string VisualInstruction => SelectedVehicle?.Type is "BICYCLE" or "SCOOTER" ? "Compara la foto y el serial registrado con el vehículo." : "Verifica visualmente que la placa del documento corresponda al vehículo.";

    public GuardAccessControlViewModel(GuardApiService api, GuardLotSession lots, IAuthSession session,
        IScannerPermission permission, IEvidenceImageValidator images)
    {
        this.api = api; this.lots = lots; this.session = session; this.permission = permission; this.images = images;
        lots.PropertyChanged += LotChanged;
        PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(State) or nameof(IsBusy) or nameof(IsInspectingEvidence) or nameof(NeedsVerification) or nameof(Evidence) or nameof(EvidenceRendered)) NotifyFlow();
            if (e.PropertyName is nameof(Access) or nameof(SelectedVehicle) or nameof(Movement))
                foreach (var name in new[] { nameof(UserSummary), nameof(VehicleSummary), nameof(MovementSummary), nameof(EvidenceLabel), nameof(VisualInstruction), nameof(CanConfirm) }) OnPropertyChanged(name);
        };
    }
    private void NotifyFlow()
    {
        foreach (var name in new[] { nameof(CameraMounted), nameof(IsDetecting), nameof(ShowManual), nameof(ShowSelection), nameof(ShowConfirmation), nameof(ShowEntry), nameof(ShowExit), nameof(ShowSuccess), nameof(ShowReset), nameof(CanConfirm), nameof(CanManual), nameof(CanReset) }) OnPropertyChanged(name);
    }
    partial void OnEvidenceChanged(byte[]? value) => EvidenceRendered = false;
    public void ReportEvidenceRendered(byte[]? bytes, bool displayed)
    {
        if (bytes is null || !ReferenceEquals(bytes, Evidence) || !ShowConfirmation) return;
        EvidenceRendered = displayed;
        if (!displayed) ErrorMessage = ShowEntry ? "No fue posible cargar la evidencia del vehículo. Intenta nuevamente." : "Evidencia no disponible. Puedes registrar la salida.";
    }
    private void LotChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (active && e.PropertyName == nameof(GuardLotSession.Selected)) Reset();
    }
    private void SessionChanged(object? sender, EventArgs e)
    {
        if (!active) return;
        var epoch = generation;
        if (uiContext is not null && SynchronizationContext.Current != uiContext) uiContext.Post(_ => { if (active && epoch == generation) Stop(); }, null);
        else Stop();
    }
    public void Stop()
    {
        active = false; generation++; Interlocked.Exchange(ref scanClaimed, 1); ClearContext(); State = GuardAccessState.INITIALIZING; NotifyFlow();
        lots.PropertyChanged -= LotChanged;
        if (session is IAuthSessionNotifications notifier) notifier.Changed -= SessionChanged;
    }
    [RelayCommand] private Task StartAsync() => WorkAsync(async () =>
    {
        generation++; var epoch = generation; active = true; lots.PropertyChanged -= LotChanged; lots.PropertyChanged += LotChanged;
        if (session is IAuthSessionNotifications notifier) { notifier.Changed -= SessionChanged; notifier.Changed += SessionChanged; }
        ClearContext(); State = GuardAccessState.INITIALIZING; cameraGranted = false; NotifyFlow();
        lots.RequireGuard(); sessionToken = await session.GetTokenAsync(); actorId = session.User?.Id;
        await lots.RefreshAsync();
        // Refresh may reset a selection; only screen departure invalidates initialization.
        if (!active || session.User?.Id != actorId || sessionToken != await session.GetTokenAsync()) { Stop(); return; }
        epoch = generation;
        cameraGranted = await permission.RequestAsync();
        if (!await CurrentAsync(epoch)) return;
        Reset();
        if (!cameraGranted) ErrorMessage = "No se concedió permiso para usar la cámara. Usa la búsqueda manual por identificación.";
    });
    private async Task<bool> CurrentAsync(int epoch)
    {
        if (!active || epoch != generation) return false;
        if (!lots.IsGuard || sessionToken is null || session.User?.Id != actorId || await session.GetTokenAsync() != sessionToken) { Stop(); return false; }
        return true;
    }
    private void ClearContext()
    {
        Access = null; SelectedVehicle = null; Movement = null; Evidence = null; Vehicles.Clear(); IsInspectingEvidence = false;
        identity = null; operationLot = null; attemptedMovement = null; NeedsVerification = false; ResultMessage = ErrorMessage = IdentificationNumber = "";
    }
    private void Reset()
    {
        generation++; ClearContext(); State = GuardAccessState.SCANNING; Interlocked.Exchange(ref scanClaimed, 0); NotifyFlow();
    }
    [RelayCommand] private void Continue() { if (active && !IsBusy && !NeedsVerification) Reset(); }
    [RelayCommand] private void Cancel() { if (active && !IsBusy && !NeedsVerification) Reset(); }
    [RelayCommand] private void Manual()
    {
        if (!CanManual) return; Reset(); Interlocked.Exchange(ref scanClaimed, 1); State = GuardAccessState.MANUAL_LOOKUP;
    }
    public Task ScanAsync(string? payload)
    {
        if (!IsDetecting || string.IsNullOrWhiteSpace(payload) || Interlocked.CompareExchange(ref scanClaimed, 1, 0) != 0) return Task.CompletedTask;
        State = GuardAccessState.PROCESSING_IDENTITY;
        return ResolveAsync(new(payload, null));
    }
    [RelayCommand] private Task SearchAsync()
    {
        if (State != GuardAccessState.MANUAL_LOOKUP || IsBusy) return Task.CompletedTask;
        if (string.IsNullOrWhiteSpace(IdentificationNumber) || IdentificationNumber.Trim().Length > 50)
        { ErrorMessage = "La identificación es obligatoria y admite hasta 50 caracteres."; return Task.CompletedTask; }
        var request = new ParkingAccessRequest(null, IdentificationNumber.Trim()); State = GuardAccessState.PROCESSING_IDENTITY;
        return ResolveAsync(request);
    }
    private Task ResolveAsync(ParkingAccessRequest request) => WorkAsync(async () =>
    {
        var epoch = generation;
        if (!await CurrentAsync(epoch)) return;
        try { operationLot = lots.RequireLot(); }
        catch (UserInputException e) { ErrorMessage = e.Message; State = GuardAccessState.ERROR; return; }
        identity = request;
        var result = await api.LookupAsync(request);
        if (!await CurrentAsync(epoch)) return;
        if (!result.IsSuccess) { ErrorMessage = GuardPresentation.AccessError(result.Error!); State = GuardAccessState.ERROR; return; }
        Access = result.Value!;
        if (Access.CurrentMovement is { } open)
        {
            if (open.Status != "OPEN") { State = GuardAccessState.ERROR; ErrorMessage = "El estado cambió. Consulta nuevamente el acceso."; return; }
            Movement = open;
            SelectedVehicle = Access.CurrentVehicle ?? new(open.VehicleId, open.VehicleType, open.VehicleIdentifier, "", "", "");
            State = GuardAccessState.CONFIRMING_EXIT; await LoadEvidenceAsync(epoch); return;
        }
        if (Access.User.Status != "ACTIVE")
        { ErrorMessage = $"El usuario está {AdminPresentation.Status(Access.User.Status).ToLowerInvariant()} y no puede ingresar."; State = GuardAccessState.ERROR; return; }
        if (Access.EligibleVehicles.Count == 0)
        { ErrorMessage = Access.EntryBlockMessage ?? "No hay vehículos habilitados para ingreso."; State = GuardAccessState.ERROR; return; }
        if (Access.EligibleVehicles.Count == 1)
        { SelectedVehicle = Access.EligibleVehicles[0]; State = GuardAccessState.CONFIRMING_ENTRY; await LoadEvidenceAsync(epoch); return; }
        foreach (var vehicle in Access.EligibleVehicles)
        {
            var image = await ReadImageAsync(vehicle);
            if (!await CurrentAsync(epoch)) return;
            Vehicles.Add(new(vehicle, image));
        }
        State = GuardAccessState.SELECTING_VEHICLE;
    });
    private async Task<byte[]?> ReadImageAsync(EligibleVehicleResponse vehicle)
    {
        if (vehicle.VerificationImage is null) return null;
        try
        {
            var result = await api.FileAsync(vehicle.VerificationImage.ContentUrl);
            return result.IsSuccess && result.Value is { Length: > 0 } bytes && await images.CanDisplayAsync(bytes) ? bytes : null;
        }
        catch (Exception) { return null; }
    }
    private async Task LoadEvidenceAsync(int epoch)
    {
        var bytes = SelectedVehicle is { } vehicle ? await ReadImageAsync(vehicle) : null;
        if (!await CurrentAsync(epoch)) return;
        Evidence = bytes;
        if (bytes is null) ErrorMessage = ShowEntry ? "No fue posible cargar la evidencia del vehículo. No puedes registrar el ingreso." : "Evidencia no disponible. Puedes registrar la salida.";
    }
    [RelayCommand] private Task SelectAsync(GuardVehicleChoice? choice) => WorkAsync(async () =>
    {
        if (State != GuardAccessState.SELECTING_VEHICLE || choice is null || !Vehicles.Contains(choice) || !await CurrentAsync(generation)) return;
        SelectedVehicle = choice.Vehicle; State = GuardAccessState.CONFIRMING_ENTRY; Evidence = choice.Image;
        if (Evidence is null) await LoadEvidenceAsync(generation);
    });
    [RelayCommand] private Task ConfirmAsync()
    {
        if (!CanConfirm) return Task.CompletedTask;
        var isExit = ShowExit; State = GuardAccessState.SUBMITTING;
        return WorkAsync(async () =>
        {
            var epoch = generation;
            if (!await CurrentAsync(epoch) || operationLot != lots.Selected?.Id) return;
            attemptedExit = isExit; attemptedMovement = isExit ? Movement!.MovementId : Guid.NewGuid();
            var result = isExit ? await api.CheckOutAsync(attemptedMovement.Value, SelectedVehicle!.Id, sessionToken) :
                await api.CheckInAsync(Access!.User.Id, SelectedVehicle!.Id, operationLot!.Value, attemptedMovement, sessionToken);
            if (!await CurrentAsync(epoch)) return;
            if (result.IsSuccess) { Success(result.Value!, isExit); return; }
            ErrorMessage = GuardPresentation.AccessError(result.Error!);
            if (GuardPresentation.Uncertain(result.Error)) { NeedsVerification = true; await VerifyCoreAsync(epoch); }
            else { State = GuardAccessState.ERROR; }
        });
    }
    private void Success(ParkingMovementResponse result, bool isExit)
    {
        Movement = result; NeedsVerification = false; ErrorMessage = ""; IsInspectingEvidence = false;
        ResultMessage = $"{(isExit ? "SALIDA REGISTRADA" : "INGRESO REGISTRADO")}\n{result.VehicleIdentifier} · {result.ParkingLotName}\n{MobileDates.Display(isExit ? result.CheckOutAtUtc!.Value : result.CheckInAtUtc)}" + (isExit ? $"\nDuración: {result.Duration}" : "");
        State = isExit ? GuardAccessState.SUCCESS_EXIT : GuardAccessState.SUCCESS_ENTRY;
    }
    [RelayCommand] private Task VerifyAsync() => WorkAsync(async () => { if (NeedsVerification && await CurrentAsync(generation)) await VerifyCoreAsync(generation); });
    private async Task VerifyCoreAsync(int epoch)
    {
        if (attemptedMovement is null) return;
        var result = await api.MovementAsync(attemptedMovement.Value);
        if (!await CurrentAsync(epoch)) return;
        var exact = result.Value;
        if (result.IsSuccess && exact?.MovementId == attemptedMovement && exact.VehicleId == SelectedVehicle?.Id &&
            (attemptedExit ? exact.Status == "CLOSED" : exact.UserId == Access?.User.Id && exact.ParkingLotId == operationLot && exact.CheckInGuardId == actorId))
        { Success(exact, attemptedExit); return; }
        NeedsVerification = true; State = GuardAccessState.ERROR;
        ErrorMessage = "No fue posible confirmar el estado. Verifica antes de intentar nuevamente.";
    }
    public Task OpenMovementAsync(ParkingMovementResponse observed) => WorkAsync(async () =>
    {
        var epoch = generation; if (!await CurrentAsync(epoch)) return;
        operationLot = lots.RequireLot(); State = GuardAccessState.PROCESSING_IDENTITY;
        var fresh = await api.MovementAsync(observed.MovementId);
        if (!await CurrentAsync(epoch)) return;
        if (!fresh.IsSuccess || fresh.Value!.Status != "OPEN" || fresh.Value.VehicleId != observed.VehicleId)
        { State = GuardAccessState.ERROR; ErrorMessage = "El estado del movimiento cambió. Consulta nuevamente los vehículos dentro."; return; }
        Movement = fresh.Value;
        var detail = await api.MovementVehicleAsync(observed.MovementId);
        if (!await CurrentAsync(epoch)) return;
        SelectedVehicle = detail.Value ?? new(observed.VehicleId, observed.VehicleType, observed.VehicleIdentifier, "", "", "");
        State = GuardAccessState.CONFIRMING_EXIT; await LoadEvidenceAsync(epoch);
    });
}
