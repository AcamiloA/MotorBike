using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using UniversityParking.Mobile.Core;
using UniversityParking.Contracts.Common;
using UniversityParking.Contracts.Vehicles;
using UniversityParking.Contracts.Parking;
using UniversityParking.Contracts.News;
using UniversityParking.Contracts.Users;

namespace UniversityParking.Mobile.ViewModels;

public abstract partial class UserFeatureViewModel : BusyViewModel
{
    protected async Task WorkAsync(Func<Task> action)
    {
        if (IsBusy) return; IsBusy = true; ErrorMessage = "";
        try { await action(); }
        catch (UserInputException exception) { ErrorMessage = exception.Message; }
        catch (Exception) { ErrorMessage = "No fue posible completar la operación. Intenta nuevamente."; }
        finally { IsBusy = false; }
    }
    protected bool Accepted<T>(ApiResult<T> result) { if (result.IsSuccess) return true; ErrorMessage = result.Error!.Message; return false; }
    protected static void Required(string value, string name, int limit = 100)
    { if (string.IsNullOrWhiteSpace(value) || value.Trim().Length > limit) throw new UserInputException($"{name} es obligatorio y admite hasta {limit} caracteres."); }
}
public abstract partial class PagedUserViewModel<T> : UserFeatureViewModel
{
    [ObservableProperty] private int page = 1;
    [ObservableProperty] private long totalPages;
    public ObservableCollection<T> Items { get; } = [];
    public bool IsEmpty => Items.Count == 0 && !IsBusy && string.IsNullOrEmpty(ErrorMessage);
    public string PageLabel => $"Página {Page} de {Math.Max(1, TotalPages)}";
    public bool CanNext => !IsBusy && Page < TotalPages;
    public bool CanPrevious => !IsBusy && Page > 1;
    protected PagedUserViewModel()
    {
        Items.CollectionChanged += (_, _) => OnPropertyChanged(nameof(IsEmpty));
        PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(IsBusy) or nameof(ErrorMessage)) OnPropertyChanged(nameof(IsEmpty));
            if (e.PropertyName is nameof(Page) or nameof(TotalPages)) OnPropertyChanged(nameof(PageLabel));
            if (e.PropertyName is nameof(IsBusy) or nameof(Page) or nameof(TotalPages)) { OnPropertyChanged(nameof(CanNext)); OnPropertyChanged(nameof(CanPrevious)); }
        };
    }
    protected void Apply(PagedResponse<T> value) { Items.Clear(); foreach (var item in value.Items) Items.Add(item); Page = value.Page; TotalPages = value.TotalPages; }
    protected abstract Task LoadPageAsync();
    [RelayCommand] private async Task RefreshAsync() { if (IsBusy) return; Page = 1; Items.Clear(); await LoadPageAsync(); }
    [RelayCommand] private async Task NextAsync() { if (!CanNext) return; var previous = Page; Page++; await LoadPageAsync(); if (!string.IsNullOrEmpty(ErrorMessage)) Page = previous; }
    [RelayCommand] private async Task PreviousAsync() { if (!CanPrevious) return; var previous = Page; Page--; await LoadPageAsync(); if (!string.IsNullOrEmpty(ErrorMessage)) Page = previous; }
}
public partial class VehicleCardViewModel(VehicleResponse vehicle, UserApiService api) : UserFeatureViewModel
{
    public VehicleResponse Vehicle => vehicle;
    public Guid Id => vehicle.Id;
    public string Identifier => vehicle.Plate ?? vehicle.FrameNumber ?? "";
    public string Heading => $"{vehicle.Brand} {vehicle.Model}";
    public string TypeIcon => VehiclePresentation.Icon(vehicle.Type);
    public string TypeName => VehiclePresentation.TypeName(vehicle.Type);
    public string Summary => $"{vehicle.Color} · {(vehicle.Status == "ACTIVE" ? "Activo" : "Inactivo")}";
    public string Registration => VehiclePresentation.RegistrationName(vehicle.RegistrationState);
    public string Location => vehicle.IsInside ? "DENTRO" : "FUERA";
    [ObservableProperty] private byte[]? photo;
    [RelayCommand] private async Task LoadPhotoAsync()
    {
        if (Photo is not null || vehicle.VerificationImagePreviewUrl is null) return;
        await WorkAsync(async () => { var result = await api.FileAsync(vehicle.VerificationImagePreviewUrl); if (Accepted(result)) Photo = result.Value; });
    }
}
public partial class MyVehiclesViewModel : UserFeatureViewModel
{
    private readonly UserApiService api; private readonly IUserNavigation navigation;
    public ObservableCollection<VehicleCardViewModel> Vehicles { get; } = [];
    public bool IsEmpty => Vehicles.Count == 0 && !IsBusy && string.IsNullOrEmpty(ErrorMessage);
    public MyVehiclesViewModel(UserApiService api, IUserNavigation navigation)
    {
        this.api = api; this.navigation = navigation;
        Vehicles.CollectionChanged += (_, _) => OnPropertyChanged(nameof(IsEmpty));
        PropertyChanged += (_, e) => { if (e.PropertyName is nameof(IsBusy) or nameof(ErrorMessage)) OnPropertyChanged(nameof(IsEmpty)); };
    }
    [RelayCommand] private Task LoadAsync() => WorkAsync(async () =>
    {
        var result = await api.MyVehiclesAsync(); if (Accepted(result)) { Vehicles.Clear(); foreach (var item in result.Value!) Vehicles.Add(new(item, api)); }
        OnPropertyChanged(nameof(IsEmpty));
    });
    [RelayCommand] private Task RegisterAsync() => navigation.GoAsync("vehicle-register");
    [RelayCommand] private Task OpenAsync(Guid id) => navigation.GoAsync("vehicle-detail", new Dictionary<string, object> { ["vehicleId"] = id });
}
public partial class UserHomeViewModel(UserApiService api, IUserNavigation navigation, IAuthSession session) : UserFeatureViewModel
{
    public string Greeting => $"Hola, {session.User?.FullName.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault()}";
    [ObservableProperty] private string activeCount = "—";
    public ObservableCollection<VehicleCardViewModel> Vehicles { get; } = [];
    public ObservableCollection<MovementCard> RecentMovements { get; } = [];
    public ObservableCollection<NewsCard> RecentNews { get; } = [];
    [RelayCommand] private Task LoadAsync() => WorkAsync(async () =>
    {
        var vehiclesTask = api.MyVehiclesAsync(); var newsTask = api.NewsAsync(); var today = DateOnly.FromDateTime(MobileDates.Today);
        var historyTask = api.HistoryAsync(today.AddDays(-30), today, null); await Task.WhenAll(vehiclesTask, newsTask, historyTask);
        var vehicles = await vehiclesTask; var news = await newsTask; var history = await historyTask;
        Vehicles.Clear(); RecentNews.Clear(); RecentMovements.Clear(); ActiveCount = "—";
        if (vehicles.IsSuccess) { ActiveCount = vehicles.Value!.Count(x => x.Status == "ACTIVE").ToString(); foreach (var item in vehicles.Value!.Take(3)) Vehicles.Add(new(item, api)); }
        if (news.IsSuccess) foreach (var item in news.Value!.Items.Take(3)) RecentNews.Add(new(item));
        if (history.IsSuccess) foreach (var item in history.Value!.Items.Take(3)) RecentMovements.Add(new(item));
        ErrorMessage = string.Join(" ", new[] { vehicles.Error?.Message, news.Error?.Message, history.Error?.Message }.Where(x => x is not null).Distinct());
        OnPropertyChanged(nameof(Greeting));
    });
    [RelayCommand] private Task RegisterAsync() => navigation.GoAsync("vehicle-register");
    [RelayCommand] private Task VehiclesAsync() => navigation.GoAsync("my-vehicles");
    [RelayCommand] private Task HistoryAsync() => navigation.GoAsync("my-history");
    [RelayCommand] private Task NewsAsync() => navigation.GoAsync("user-news");
    [RelayCommand] private Task ProfileAsync() => navigation.GoAsync("user-profile");
    [RelayCommand] private Task OpenVehicleAsync(Guid id) => navigation.GoAsync("vehicle-detail", new Dictionary<string, object> { ["vehicleId"] = id });
    [RelayCommand] private Task OpenNewsAsync(NewsCard card) => navigation.GoAsync("news-detail", new Dictionary<string, object> { ["news"] = card.Item });
}
public partial class VehicleDetailViewModel(UserApiService api, IUserNavigation navigation, IFileViewer viewer, IAuthSession session) : UserFeatureViewModel
{
    public Guid VehicleId { get; set; }
    [ObservableProperty] private VehicleDetailResponse? detail;
    [ObservableProperty] private byte[]? photo;
    [ObservableProperty] private string period = "Sin periodo académico activo";
    public string Identifier => Detail?.Vehicle.Plate ?? Detail?.Vehicle.FrameNumber ?? "";
    public string Heading => Detail is null ? "" : $"{Detail.Vehicle.Brand} {Detail.Vehicle.Model}";
    public string Summary => Detail is null ? "" : $"{VehiclePresentation.TypeName(Detail.Vehicle.Type)} · {Detail.Vehicle.Color} · {(Detail.Vehicle.Status == "ACTIVE" ? "Activo" : "Inactivo")}";
    public string Registration => Detail is null ? "" : VehiclePresentation.RegistrationName(Detail.Vehicle.RegistrationState);
    public string Location => Detail?.Vehicle.IsInside == true ? "DENTRO" : "FUERA";
    public bool IsOwner => Detail?.Vehicle.CurrentOwnerId == session.User?.Id;
    public bool CanRenew => IsOwner && Detail?.Vehicle.Status == "ACTIVE" && Detail.Vehicle.RegistrationState is "NONE" or "EXPIRED";
    public string StatusAction => Detail?.Vehicle.Status == "ACTIVE" ? "DESACTIVAR" : "ACTIVAR";
    public string VerificationLabel => VehiclePresentation.VerificationLabel(Detail?.Vehicle.Type);
    public string VerificationPending => Detail?.VerificationImage is null ? "Evidencia de verificación pendiente." : "";
    public bool CanUpdateVerification => Detail is not null && (IsOwner || session.User?.Roles.Contains("ADMIN") == true)
        && !(session.User?.Roles.Contains("GUARD") == true && session.User?.Roles.Contains("ADMIN") != true);
    [RelayCommand] private Task UpdateVerificationAsync() => navigation.GoAsync("vehicle-verification", new Dictionary<string, object> { ["vehicleId"] = VehicleId });
    public ObservableCollection<VehicleDocumentResponse> Documents { get; } = [];
    [RelayCommand] private Task LoadAsync() => WorkAsync(async () =>
    {
        if (VehicleId == Guid.Empty) throw new UserInputException("Selecciona un vehículo.");
        var result = await api.VehicleAsync(VehicleId); if (!Accepted(result)) return;
        Detail = result.Value; Documents.Clear(); foreach (var document in Detail!.Documents) Documents.Add(document);
        foreach (var name in new[] { nameof(Identifier), nameof(Heading), nameof(Summary), nameof(Registration), nameof(Location), nameof(CanRenew), nameof(IsOwner), nameof(StatusAction), nameof(VerificationLabel), nameof(VerificationPending), nameof(CanUpdateVerification) }) OnPropertyChanged(name);
        var current = await api.CurrentPeriodAsync(); Period = current.IsSuccess ? current.Value!.Name : current.Error!.HttpStatus == 404 ? "Sin periodo académico activo" : "No fue posible consultar el periodo";
        var general = Detail.VerificationImage; Photo = null;
        if (general is not null) { var file = await api.FileAsync(general.ContentUrl); if (Accepted(file)) Photo = file.Value; }
    });
    [RelayCommand] private Task EditAsync() => navigation.GoAsync("vehicle-edit", new Dictionary<string, object> { ["vehicleId"] = VehicleId });
    [RelayCommand] private Task RenewAsync() => navigation.GoAsync("vehicle-renew", new Dictionary<string, object> { ["vehicleId"] = VehicleId });
    [RelayCommand] private Task ChangeStatusAsync() => WorkAsync(async () =>
    {
        if (Detail is null || !IsOwner) return;
        var activate = Detail.Vehicle.Status != "ACTIVE";
        if (!activate && !await navigation.ConfirmAsync("Desactivar vehículo", "¿Deseas desactivar este vehículo?")) return;
        if (!Accepted(await api.VehicleStatusAsync(VehicleId, activate))) return;
        await navigation.MessageAsync("Vehículo", activate ? "Vehículo activado correctamente." : "Vehículo desactivado correctamente.");
        var refreshed = await api.VehicleAsync(VehicleId); if (Accepted(refreshed)) { Detail = refreshed.Value; foreach (var name in new[] { nameof(Summary), nameof(CanRenew), nameof(StatusAction), nameof(VerificationLabel), nameof(VerificationPending), nameof(CanUpdateVerification) }) OnPropertyChanged(name); }
    });
    [RelayCommand] private Task OpenDocumentAsync(VehicleDocumentResponse document) => WorkAsync(async () =>
    {
        var token = await session.GetTokenAsync(); if (token is null) return;
        var file = await api.FileAsync(document.ContentUrl);
        if (Accepted(file) && session.User is not null && await session.GetTokenAsync() == token)
            await viewer.OpenAsync(document.OriginalFileName, document.ContentType, file.Value!);
    });
}
public partial class DocumentInputViewModel(string type, IAttachmentPicker picker, string? existingName = null) : UserFeatureViewModel
{
    public string Type => type;
    public string Label => VehiclePresentation.DocumentName(type);
    public bool HasExisting => existingName is not null;
    public string ExistingLabel => existingName is null ? "Archivo requerido" : $"Actual: {existingName}. Reemplazo opcional.";
    [ObservableProperty, NotifyPropertyChangedFor(nameof(FileName))] private PickedAttachment? file;
    [ObservableProperty] private string number = "";
    [ObservableProperty] private bool hasIssuedOn;
    [ObservableProperty] private bool hasExpiresOn;
    [ObservableProperty] private DateTime issuedOn = MobileDates.Today;
    [ObservableProperty] private DateTime expiresOn = MobileDates.Today.AddYears(1);
    public string FileName => File?.FileName ?? "Ningún archivo seleccionado";
    [RelayCommand] private Task PickAsync() => WorkAsync(async () => { var picked = await picker.DocumentAsync(); if (picked is not null) File = picked; });
    public DocumentAttachment? Build()
    {
        if (HasIssuedOn && HasExpiresOn && ExpiresOn.Date < IssuedOn.Date) throw new UserInputException($"{Label}: la fecha de vencimiento debe ser posterior o igual a la emisión.");
        return File is null ? null : new(Type, File, Number, HasIssuedOn ? DateOnly.FromDateTime(IssuedOn) : null, HasExpiresOn ? DateOnly.FromDateTime(ExpiresOn) : null);
    }
}
public partial class RegisterVehicleViewModel : UserFeatureViewModel
{
    private readonly UserApiService api; private readonly IAuthSession session; private readonly IAttachmentPicker picker; private readonly IUserNavigation navigation;
    [ObservableProperty] private string processingMessage = "";
    public ObservableCollection<TypeChoice> Types { get; } = [];
    [ObservableProperty, NotifyPropertyChangedFor(nameof(IdentifierLabel))] private TypeChoice? selectedType;
    [ObservableProperty] private string identifier = "";
    [ObservableProperty] private string brand = "";
    [ObservableProperty] private string model = "";
    [ObservableProperty] private string color = "";
    [ObservableProperty, NotifyPropertyChangedFor(nameof(VerificationImageName))] private PickedAttachment? verificationImage;
    public string IdentifierLabel => SelectedType?.Code == "BICYCLE" ? "Número de marco" : "Placa";
    public string VerificationImageName => VerificationImage?.FileName ?? "Selecciona una imagen JPEG/PNG (máximo 5 MB).";
    public string VerificationLabel => VehiclePresentation.VerificationLabel(SelectedType?.Code);
    public string VerificationHelp => VehiclePresentation.VerificationHelp(SelectedType?.Code);
    public RegisterVehicleViewModel(UserApiService api, IAuthSession session, IAttachmentPicker picker, IUserNavigation navigation)
    { this.api = api; this.session = session; this.picker = picker; this.navigation = navigation; ApplyTypes(); }
    private void ApplyTypes()
    { Types.Clear(); foreach (var code in VehiclePresentation.AllowedTypes(session.User?.MemberType)) Types.Add(new(code, VehiclePresentation.TypeName(code))); SelectedType = Types[0]; }
    partial void OnSelectedTypeChanged(TypeChoice? value)
    { Identifier = ""; VerificationImage = null; OnPropertyChanged(nameof(VerificationLabel)); OnPropertyChanged(nameof(VerificationHelp)); }
    [RelayCommand] private Task LoadAsync() => WorkAsync(async () =>
    { var result = await api.ProfileAsync(); if (Accepted(result) && await session.GetTokenAsync() is { } token) { await session.TrySetUserAsync(token, result.Value!); ApplyTypes(); } });
    [RelayCommand] private Task PickPhotoAsync(bool camera) => WorkAsync(async () => { var value = await picker.PhotoAsync(camera); if (value is not null) VerificationImage = AttachmentValidation.Validate(value.FileName, value.ContentType, value.Bytes, true); });
    [RelayCommand] private Task SaveAsync() => WorkAsync(async () =>
    {
        var type = SelectedType?.Code ?? "";
        if (!VehiclePresentation.AllowedTypes(session.User?.MemberType).Contains(type)) throw new UserInputException("Selecciona un tipo de vehículo permitido.");
        Required(Identifier, IdentifierLabel, type == "BICYCLE" ? 150 : 100); Required(Brand, "Marca"); Required(Model, "Modelo"); Required(Color, "Color");
        if (VerificationImage is null) throw new UserInputException("Selecciona la evidencia de verificación.");
        ProcessingMessage = type == "BICYCLE" ? "Guardando vehículo..." : "Validando Licencia de Tránsito...";
        try
        {
        var result = await api.RegisterAsync(new(type, Identifier, Brand, Model, Color, VerificationImage)); if (!Accepted(result)) { if (TransitLicensePresentation.DocumentError(result.Error!.Code)) VerificationImage = null; return; }
        VerificationImage = null;
        await navigation.MessageAsync("Vehículo", "Vehículo registrado correctamente.");
        await navigation.BackAsync(); await navigation.GoAsync("vehicle-detail", new Dictionary<string, object> { ["vehicleId"] = result.Value!.Id });
        }
        finally { ProcessingMessage = ""; }
    });
}
public partial class EditVehicleViewModel(UserApiService api, IUserNavigation navigation) : UserFeatureViewModel
{
    public Guid VehicleId { get; set; }
    [ObservableProperty] private string brand = "";
    [ObservableProperty] private string model = "";
    [ObservableProperty] private string color = "";
    [ObservableProperty] private string identifier = "";
    [RelayCommand] private Task LoadAsync() => WorkAsync(async () =>
    { var result = await api.VehicleAsync(VehicleId); if (Accepted(result)) { var vehicle = result.Value!.Vehicle; Brand = vehicle.Brand; Model = vehicle.Model; Color = vehicle.Color; Identifier = vehicle.Plate ?? vehicle.FrameNumber ?? ""; } });
    [RelayCommand] private Task SaveAsync() => WorkAsync(async () =>
    {
        Required(Brand, "Marca"); Required(Model, "Modelo"); Required(Color, "Color");
        if (!Accepted(await api.EditVehicleAsync(VehicleId, Brand.Trim(), Model.Trim(), Color.Trim()))) return;
        await navigation.MessageAsync("Vehículo", "Información actualizada correctamente."); await navigation.BackAsync();
    });
}
public partial class RenewRegistrationViewModel(UserApiService api, IUserNavigation navigation) : UserFeatureViewModel
{
    public Guid VehicleId { get; set; }
    public ObservableCollection<DocumentInputViewModel> Documents { get; } = [];
    [ObservableProperty] private string heading = "";
    [ObservableProperty] private string period = "";
    [RelayCommand] private Task LoadAsync() => WorkAsync(async () =>
    {
        Period = "";
        var result = await api.VehicleAsync(VehicleId); if (!Accepted(result)) return;
        Heading = result.Value!.Vehicle.Plate ?? result.Value.Vehicle.FrameNumber ?? ""; Documents.Clear();
        if (result.Value.VerificationImage is null) { ErrorMessage = "Evidencia de verificación pendiente. Actualiza la imagen antes de renovar."; return; }
        var current = await api.CurrentPeriodAsync(); if (Accepted(current)) Period = current.Value!.Name;
    });
    [RelayCommand] private Task SaveAsync() => WorkAsync(async () =>
    {
        if (string.IsNullOrEmpty(Period)) throw new UserInputException("Carga el vehículo con evidencia de verificación y el periodo actual antes de renovar.");
        if (!Accepted(await api.RenewAsync(VehicleId, []))) return;
        await navigation.MessageAsync("Registro", "Registro renovado correctamente."); await navigation.BackAsync();
    });
}
public sealed record MovementCard(ParkingMovementResponse Item)
{
    public string Heading => $"{VehiclePresentation.TypeName(Item.VehicleType)} · {Item.VehicleIdentifier}";
    public string Entry => $"Entrada: {MobileDates.Display(Item.CheckInAtUtc)}";
    public string Exit => Item.CheckOutAtUtc.HasValue ? $"Salida: {MobileDates.Display(Item.CheckOutAtUtc.Value)}" : "Salida: —";
    public string Status => Item.Status == "OPEN" ? "DENTRO" : "SALIDA";
    public string Summary => $"{Item.ParkingLotName} · Duración: {(int)Item.Duration.TotalHours} h {Item.Duration.Minutes} min";
}
public sealed record VehicleFilter(Guid? Id, string Label);
public partial class MyHistoryViewModel(UserApiService api) : PagedUserViewModel<MovementCard>
{
    [ObservableProperty] private DateTime dateFrom = MobileDates.Today.AddMonths(-1);
    [ObservableProperty] private DateTime dateTo = MobileDates.Today;
    [ObservableProperty] private VehicleFilter? selectedVehicle;
    public ObservableCollection<VehicleFilter> Filters { get; } = [new(null, "Todos los vehículos")];
    protected override Task LoadPageAsync() => WorkAsync(async () =>
    {
        if (DateFrom.Date > DateTo.Date) throw new UserInputException("La fecha inicial no puede superar la final.");
        var result = await api.HistoryAsync(DateOnly.FromDateTime(DateFrom), DateOnly.FromDateTime(DateTo), SelectedVehicle?.Id, Page);
        if (!Accepted(result)) return;
        Apply(new(result.Value!.Items.Select(x => new MovementCard(x)).ToArray(), result.Value.Page, result.Value.PageSize, result.Value.TotalCount, result.Value.TotalPages));
        foreach (var item in result.Value.Items.DistinctBy(x => x.VehicleId)) if (!Filters.Any(x => x.Id == item.VehicleId)) Filters.Add(new(item.VehicleId, item.VehicleIdentifier));
    });
}
public sealed record NewsCard(NewsResponse Item)
{
    public string Title => Item.Title;
    public string Published => Item.PublishedAt.HasValue ? MobileDates.Display(Item.PublishedAt.Value) : "";
    public string Preview => Item.Content.Length > 160 ? Item.Content[..160] + "…" : Item.Content;
}
public partial class NewsViewModel(UserApiService api, IUserNavigation navigation) : PagedUserViewModel<NewsCard>
{
    protected override Task LoadPageAsync() => WorkAsync(async () =>
    { var result = await api.NewsAsync(Page); if (Accepted(result)) Apply(new(result.Value!.Items.Select(x => new NewsCard(x)).ToArray(), result.Value.Page, result.Value.PageSize, result.Value.TotalCount, result.Value.TotalPages)); });
    [RelayCommand] private Task OpenAsync(NewsCard card) => navigation.GoAsync("news-detail", new Dictionary<string, object> { ["news"] = card.Item });
}
public partial class NewsDetailViewModel : ObservableObject
{
    [ObservableProperty] private NewsResponse? item;
    public string Published => Item?.PublishedAt is { } date ? MobileDates.Display(date) : "";
    partial void OnItemChanged(NewsResponse? value) => OnPropertyChanged(nameof(Published));
}
public partial class ProfileViewModel(UserApiService api, IAuthSession session, IUserNavigation navigation, AuthService auth) : UserFeatureViewModel
{
    [ObservableProperty] private UserProfileResponse? profile;
    public string MemberType => Profile?.MemberType switch { "STUDENT" => "Estudiante", "TEACHER" => "Docente", "STAFF" => "Personal universitario", _ => "" };
    public string Roles => string.Join(" · ", (Profile?.Roles ?? []).Select(RoleNavigation.Title));
    [RelayCommand] private Task LoadAsync() => WorkAsync(async () =>
    { var result = await api.ProfileAsync(); if (!Accepted(result)) return; Profile = result.Value; OnPropertyChanged(nameof(MemberType)); OnPropertyChanged(nameof(Roles)); if (await session.GetTokenAsync() is { } token) await session.TrySetUserAsync(token, Profile!); });
    [RelayCommand] private Task EditAsync() => navigation.GoAsync("profile-edit");
    [RelayCommand] private Task PasswordAsync() => navigation.GoAsync("password-change");
    [RelayCommand] private Task LogoutAsync() => WorkAsync(auth.LogoutAsync);
}
public partial class EditProfileViewModel(UserApiService api, IUserNavigation navigation) : UserFeatureViewModel
{
    [ObservableProperty] private string fullName = "";
    [ObservableProperty] private string career = "";
    [RelayCommand] private Task LoadAsync() => WorkAsync(async () => { var result = await api.ProfileAsync(); if (Accepted(result)) { FullName = result.Value!.FullName; Career = result.Value.Career ?? ""; } });
    [RelayCommand] private Task SaveAsync() => WorkAsync(async () =>
    { Required(FullName, "Nombre", 200); if (!Accepted(await api.EditProfileAsync(FullName.Trim(), string.IsNullOrWhiteSpace(Career) ? null : Career.Trim()))) return; await navigation.MessageAsync("Perfil", "Perfil actualizado correctamente."); await navigation.BackAsync(); });
}
public partial class ChangePasswordViewModel(UserApiService api, IUserNavigation navigation) : UserFeatureViewModel
{
    [ObservableProperty] private string currentPassword = "";
    [ObservableProperty] private string newPassword = "";
    [ObservableProperty] private string confirmation = "";
    [RelayCommand] private Task SaveAsync() => WorkAsync(async () =>
    {
        try
        {
            if (string.IsNullOrWhiteSpace(CurrentPassword) || NewPassword != Confirmation || !PasswordPresentation.IsValid(NewPassword))
                throw new UserInputException("Completa la contraseña actual y confirma una nueva de al menos 8 caracteres con mayúscula, minúscula y número.");
            if (!Accepted(await api.ChangePasswordAsync(CurrentPassword, NewPassword))) return;
            await navigation.MessageAsync("Contraseña", "Contraseña actualizada correctamente."); await navigation.BackAsync();
        }
        finally { CurrentPassword = ""; NewPassword = ""; Confirmation = ""; }
    });
}
