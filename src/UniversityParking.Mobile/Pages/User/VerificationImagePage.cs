using UniversityParking.Mobile.ViewModels;

namespace UniversityParking.Mobile.Pages.User;

public sealed class VerificationImagePage : UserPage<VerificationImageViewModel>
{
    public VerificationImagePage(VerificationImageViewModel vm)
        : base(vm, "Evidencia de verificación", () => vm.LoadCommand.ExecuteAsync(null), once: true)
    {
        var camera = UserViews.Button("TOMAR FOTO", "PickCommand"); camera.CommandParameter = true;
        var gallery = UserViews.Button("SELECCIONAR IMAGEN", "PickCommand"); gallery.CommandParameter = false;
        Form(UserViews.Heading("EVIDENCIA DE VERIFICACIÓN"), UserViews.Bound("VerificationLabel"),
            UserViews.Bound("VerificationHelp", true), UserViews.Bound("ImageName", true),
            UserViews.Image("VerificationImage", 220), camera, gallery, UserViews.Button("GUARDAR EVIDENCIA", "SaveCommand"));
    }
    public override void ApplyQueryAttributes(IDictionary<string, object> query) => ViewModel.VehicleId = VehicleId(query);
    protected override void OnDisappearing() { ViewModel.Leave(); base.OnDisappearing(); }
}
