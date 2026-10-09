using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using UniversityParking.Mobile.Core;

namespace UniversityParking.Mobile.ViewModels;

public partial class VerificationImageViewModel(UserApiService api, IAttachmentPicker picker, IAuthSession session,
    IUserNavigation navigation) : UserFeatureViewModel
{
    public Guid VehicleId { get; set; }
    [ObservableProperty] private string vehicleType = "";
    [ObservableProperty] private PickedAttachment? verificationImage;
    [ObservableProperty] private bool canEdit;
    [ObservableProperty] private bool writeUncertain;
    [ObservableProperty] private string processingMessage = "";
    private Guid? loadedUserId;
    private int lifecycle;
    public void Leave() { lifecycle++; VerificationImage = null; CanEdit = false; }
    public string VerificationLabel => VehiclePresentation.VerificationLabel(VehicleType);
    public string VerificationHelp => VehiclePresentation.VerificationHelp(VehicleType);
    public string ImageName => VerificationImage?.FileName ?? "Selecciona una imagen JPEG/PNG de máximo 5 MB.";
    partial void OnVerificationImageChanged(PickedAttachment? value) => OnPropertyChanged(nameof(ImageName));
    partial void OnVehicleTypeChanged(string value) { OnPropertyChanged(nameof(VerificationLabel)); OnPropertyChanged(nameof(VerificationHelp)); }
    [RelayCommand] private Task LoadAsync() => WorkAsync(async () =>
    {
        CanEdit = false; VerificationImage = null;
        var version = lifecycle; loadedUserId = session.User?.Id;
        var result = await api.VehicleAsync(VehicleId); if (version != lifecycle || loadedUserId != session.User?.Id || !Accepted(result)) return;
        VehicleType = result.Value!.Vehicle.Type;
        var roles = session.User?.Roles ?? [];
        CanEdit = session.User is not null && (result.Value.Vehicle.CurrentOwnerId == session.User.Id || roles.Contains("ADMIN"))
            && !(roles.Contains("GUARD") && !roles.Contains("ADMIN"));
        if (!CanEdit) ErrorMessage = "No tienes permiso para modificar esta evidencia.";
    });
    [RelayCommand] private Task PickAsync(bool camera) => WorkAsync(async () =>
    {
        if (!CanEdit || WriteUncertain) return;
        var version = lifecycle;
        var image = camera && VehicleType != "BICYCLE" ? await picker.TransitLicenseAsync() : await picker.PhotoAsync(camera);
        if (image is not null && version == lifecycle && loadedUserId == session.User?.Id)
            VerificationImage = AttachmentValidation.Validate(image.FileName, image.ContentType, image.Bytes, true);
    });
    [RelayCommand] private Task SaveAsync() => WorkAsync(async () =>
    {
        if (!CanEdit || WriteUncertain) return;
        if (VerificationImage is null) throw new UserInputException("Selecciona la evidencia de verificación.");
        var version = lifecycle;
        if (!await navigation.ConfirmAsync("Actualizar evidencia", "¿Guardar esta imagen como la única evidencia de verificación?")) return;
        var roles = session.User?.Roles ?? [];
        if (version != lifecycle || loadedUserId is null || loadedUserId != session.User?.Id || roles.Contains("GUARD") && !roles.Contains("ADMIN"))
        { Leave(); throw new UserInputException("La sesión cambió. Vuelve a consultar el vehículo."); }
        ProcessingMessage = VehicleType == "BICYCLE" ? "Guardando imagen..." : "Guardando evidencia...";
        try
        {
        var result = await api.UpdateVerificationImageAsync(VehicleId, VerificationImage);
        if (version != lifecycle || loadedUserId != session.User?.Id) return;
        if (!Accepted(result))
        {
            if (TransitLicensePresentation.DocumentError(result.Error!.Code)) VerificationImage = null;
            if (result.Error.Code != "DOCUMENT_OCR_UNAVAILABLE" && GuardPresentation.Uncertain(result.Error)) { WriteUncertain = true; ErrorMessage = "No se confirmó el cambio. Consulta el detalle antes de repetirlo."; }
            return;
        }
        VerificationImage = null; CanEdit = false;
        await navigation.MessageAsync("Evidencia", "Evidencia de verificación actualizada."); await navigation.BackAsync();
        }
        finally { ProcessingMessage = ""; }
    });
}
