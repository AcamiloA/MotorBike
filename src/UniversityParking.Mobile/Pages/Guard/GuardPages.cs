using Microsoft.Extensions.DependencyInjection;
using UniversityParking.Mobile.Core;
using UniversityParking.Mobile.ViewModels;
using UniversityParking.Mobile.Pages.User;
using UniversityParking.Contracts.Incidents;
using UniversityParking.Contracts.Parking;
using ZXing.Net.Maui;
using ZXing.Net.Maui.Controls;

namespace UniversityParking.Mobile.Pages.Guard;

internal static class GuardViews
{
    public static Picker Picker(string items, string selected, string display = "Label", string title = "Seleccionar")
    { var picker = new Picker { Title = title, ItemDisplayBinding = new Binding(display) }; picker.SetBinding(Microsoft.Maui.Controls.Picker.ItemsSourceProperty, items); picker.SetBinding(Microsoft.Maui.Controls.Picker.SelectedItemProperty, selected); picker.SetBinding(VisualElement.IsEnabledProperty, "IsNotBusy"); return picker; }
    public static View Lot() => UserViews.Input(Picker("Lots.Lots", "Lots.Selected", "Name", "Parqueadero activo"));
    public static DatePicker Date(string property) { var date = new DatePicker(); date.SetBinding(DatePicker.DateProperty, property); date.SetBinding(VisualElement.IsEnabledProperty, "IsNotBusy"); return date; }
    public static View Dates() => UserViews.Stack(UserViews.Text("Desde (Bogotá)"), Date("DateFrom"), UserViews.Text("Hasta (Bogotá)"), Date("DateTo"));
    public static View MovementList(object vm, bool actions)
    {
        return UserViews.List("Items", new DataTemplate(() =>
        {
            var stack = UserViews.Stack(UserViews.Bound("Heading"), UserViews.Bound("Summary", true), UserViews.Bound("Entry", true), UserViews.Bound("Exit", true), UserViews.Bound("Status"));
            if (actions) { stack.Children.Add(UserViews.Button("REGISTRAR SALIDA", "ExitCommand", vm, ".")); stack.Children.Add(UserViews.Button("REGISTRAR INCIDENTE", "IncidentCommand", vm, ".")); }
            return UserViews.Card(stack);
        }));
    }
    public static View ListLayout(View filters, View list, object vm)
    { var grid = new Grid { RowSpacing = 10, RowDefinitions = new RowDefinitionCollection { new() { Height = GridLength.Auto }, new() { Height = GridLength.Star }, new() { Height = GridLength.Auto } } }; grid.Add(filters,0,0); grid.Add(UserViews.Refresh(UserViews.EmptyList(list, "No hay resultados con estos filtros."), "RefreshCommand"),0,1); grid.Add(UserViews.Pager(vm),0,2); return grid; }
}
public sealed class GuardHomePage : UserPage<GuardHomeViewModel>
{
    public GuardHomePage(GuardHomeViewModel vm) : base(vm, "Control de acceso", () => vm.LoadCommand.ExecuteAsync(null))
    {
        var lot = GuardViews.Picker("Lots.Lots", "Lots.Selected", "Name", "Parqueadero activo");
        lot.SelectedIndexChanged += async (_, _) => { if (!vm.IsBusy) await vm.UpdateCommand.ExecuteAsync(null); };
        Form(UserViews.Input(lot), UserViews.Button("CONSULTAR ESTADO", "UpdateCommand"), UserViews.Bound("Summary"),
        UserViews.Button("ESCANEAR CARNÉ", "ScanCommand"), UserViews.Button("BUSCAR POR IDENTIFICACIÓN", "ManualCommand"), UserViews.Button("VER VEHÍCULOS DENTRO", "InsideCommand"),
        UserViews.Button("REGISTRAR INCIDENTE", "CreateIncidentCommand"), UserViews.Button("INCIDENTES", "IncidentsCommand"), UserViews.Button("HISTORIAL", "HistoryCommand"), UserViews.Button("ACTUALIZAR PARQUEADEROS", "LoadCommand"), UserViews.Button("CERRAR SESIÓN", "LogoutCommand"));
    }
}
public sealed class ScanCardPage : UserPage<GuardLookupViewModel>
{
    private CameraBarcodeReaderView? camera; private readonly VerticalStackLayout holder = new(); private bool active;
    public ScanCardPage(GuardLookupViewModel vm) : base(vm, "Escanear carné")
    {
        holder.HeightRequest = 300;
        Form(UserViews.Text("Apunta al código del carné. Su contenido se utiliza únicamente para consultar acceso."), holder,
            UserViews.Button("ACTIVAR / REINTENTAR CÁMARA", "StartCommand"), UserViews.Button("BUSCAR POR IDENTIFICACIÓN", "ManualCommand"));
        vm.PropertyChanged += (_, e) => { if (e.PropertyName == nameof(vm.IsScanning)) MainThread.BeginInvokeOnMainThread(UpdateCamera); };
    }
    private void UpdateCamera()
    {
        if (!active || !ViewModel.IsScanning) { Release(); return; }
        if (camera is null)
        {
            camera = new CameraBarcodeReaderView { Options = new BarcodeReaderOptions { Formats = BarcodeFormats.All, AutoRotate = true, Multiple = false }, CameraLocation = CameraLocation.Rear, IsDetecting = true };
            camera.BarcodesDetected += Detected; holder.Children.Add(camera);
        }
        camera.IsDetecting = true;
    }
    private void Detected(object? sender, BarcodeDetectionEventArgs e)
    {
        var code = e.Results.FirstOrDefault()?.Value;
        MainThread.BeginInvokeOnMainThread(async () => { if (active) await ViewModel.ScanAsync(code); });
    }
    private void Release() { if (camera is null) return; camera.IsDetecting = false; camera.IsTorchOn = false; camera.BarcodesDetected -= Detected; holder.Children.Remove(camera); camera.Handler?.DisconnectHandler(); camera = null; }
    protected override async void OnAppearing() { base.OnAppearing(); active = true; if (Window is { } window) { window.Stopped += Stopped; } await ViewModel.StartCommand.ExecuteAsync(null); }
    private void Stopped(object? sender, EventArgs e) { ViewModel.Stop(); Release(); }
    protected override void OnDisappearing() { active = false; ViewModel.Stop(); Release(); if (Window is { } window) { window.Stopped -= Stopped; } base.OnDisappearing(); }
}
public sealed class ManualSearchPage : UserPage<GuardLookupViewModel>
{
    public ManualSearchPage(GuardLookupViewModel vm) : base(vm, "Buscar por identificación") => Form(UserViews.Input(UserViews.Entry("IdentificationNumber", "Número de identificación")), UserViews.Button("BUSCAR", "SearchCommand"));
    protected override void OnDisappearing() { ViewModel.Stop(); base.OnDisappearing(); }
}
public sealed class AccessResultPage : UserPage<AccessResultViewModel>
{
    public AccessResultPage(AccessResultViewModel vm) : base(vm, "Resultado de acceso")
    {
        var enter = UserViews.Button("REGISTRAR INGRESO", "EnterCommand"); enter.SetBinding(IsVisibleProperty, "CanEnter");
        var exit = UserViews.Button("REGISTRAR SALIDA", "ExitCommand"); exit.SetBinding(IsVisibleProperty, "CanExit");
        var vehicles = GuardViews.Picker("Vehicles", "SelectedVehicle", "Identifier", "Vehículo habilitado"); vehicles.SetBinding(IsVisibleProperty,"ShowVehicles");
        Form(UserViews.Bound("UserSummary"), UserViews.Bound("MovementSummary"), vehicles, UserViews.Bound("EmptyMessage"), enter, exit);
    }
    public override void ApplyQueryAttributes(IDictionary<string, object> query) { if (query.TryGetValue("access", out var value) && value is AccessContext access) ViewModel.Access = access; }
}
public sealed class CheckInPage : UserPage<CheckInViewModel>
{
    public CheckInPage(CheckInViewModel vm) : base(vm, "Registrar ingreso", () => vm.LoadCommand.ExecuteAsync(null), once: true)
    {
        var confirm = UserViews.Button("CONFIRMAR INGRESO", "ConfirmCommand"); confirm.SetBinding(IsEnabledProperty, "CanSubmit");
        Form(UserViews.Bound("Summary"), GuardViews.Lot(), confirm, UserViews.Bound("ResultMessage"), UserViews.Button("VERIFICAR ESTADO", "VerifyCommand"), UserViews.Button("FINALIZAR", "FinishCommand"));
    }
    public override void ApplyQueryAttributes(IDictionary<string, object> query) { if (query.TryGetValue("entry", out var value) && value is EntryContext entry) ViewModel.Entry = entry; }
}
public sealed class CheckOutPage : UserPage<CheckOutViewModel>
{
    public CheckOutPage(CheckOutViewModel vm) : base(vm, "Registrar salida")
    {
        var confirm = UserViews.Button("CONFIRMAR SALIDA", "ConfirmCommand"); confirm.SetBinding(IsEnabledProperty, "CanSubmit");
        Form(UserViews.Bound("Summary"), confirm, UserViews.Bound("ResultMessage"), UserViews.Button("VERIFICAR ESTADO", "VerifyCommand"), UserViews.Button("FINALIZAR", "FinishCommand"));
    }
    public override void ApplyQueryAttributes(IDictionary<string, object> query) { if (query.TryGetValue("movement", out var value) && value is ParkingMovementResponse movement) ViewModel.Movement = movement; }
}
public sealed class VehiclesInsidePage : UserPage<VehiclesInsideViewModel>
{
    public VehiclesInsidePage(VehiclesInsideViewModel vm) : base(vm, "Vehículos dentro", vm.LoadAsync) => Layout(GuardViews.ListLayout(UserViews.Stack(GuardViews.Lot(), GuardViews.Picker("Types", "SelectedType"), UserViews.Input(UserViews.Entry("Search", "Buscar vehículo o usuario")), UserViews.Button("FILTRAR", "RefreshCommand"), UserViews.Bound("Counts")), GuardViews.MovementList(vm, true), vm));
}
public sealed class ParkingHistoryPage : UserPage<ParkingHistoryViewModel>
{
    public ParkingHistoryPage(ParkingHistoryViewModel vm) : base(vm, "Historial de parqueo", vm.LoadAsync)
    {
        var filters = new ScrollView { HeightRequest = 280, Content = UserViews.Stack(GuardViews.Lot(), GuardViews.Dates(), GuardViews.Picker("Types", "SelectedType"), GuardViews.Picker("States", "SelectedState"), UserViews.Input(UserViews.Entry("Identification", "Identificación")), UserViews.Input(UserViews.Entry("Plate", "Placa")), UserViews.Input(UserViews.Entry("Frame", "Número de marco")), UserViews.Button("FILTRAR", "RefreshCommand")) };
        Layout(GuardViews.ListLayout(filters, GuardViews.MovementList(vm, false), vm));
    }
}
public sealed class IncidentsPage : UserPage<IncidentsViewModel>
{
    public IncidentsPage(IncidentsViewModel vm) : base(vm, "Incidentes", vm.LoadAsync)
    {
        var filters = new ScrollView { HeightRequest = 250, Content = UserViews.Stack(GuardViews.Lot(), GuardViews.Dates(), GuardViews.Picker("Types", "SelectedType"), GuardViews.Picker("States", "SelectedState"), UserViews.Button("FILTRAR", "RefreshCommand"), UserViews.Button("REGISTRAR INCIDENTE", "CreateCommand")) };
        var list = UserViews.List("Items", new DataTemplate(() => UserViews.Card(UserViews.Stack(UserViews.Bound("Heading"), UserViews.Bound("Description"), UserViews.Bound("Date",true), UserViews.Button("VER DETALLE", "OpenCommand",vm,".")))));
        Layout(GuardViews.ListLayout(filters,list,vm));
    }
}
public sealed class CreateIncidentPage : UserPage<CreateIncidentViewModel>
{
    public CreateIncidentPage(CreateIncidentViewModel vm) : base(vm, "Registrar incidente", () => vm.LoadCommand.ExecuteAsync(null), once:true)
    {
        var occurred = new CheckBox(); occurred.SetBinding(CheckBox.IsCheckedProperty,"HasOccurredAt"); var date = GuardViews.Date("OccurredDate"); date.SetBinding(IsVisibleProperty,"HasOccurredAt");
        var time = new TimePicker(); time.SetBinding(TimePicker.TimeProperty,"OccurredTime"); time.SetBinding(IsVisibleProperty,"HasOccurredAt");
        var description = new Editor { Placeholder = "Descripción", AutoSize = EditorAutoSizeOption.TextChanges, MinimumHeightRequest = 110 }; description.SetBinding(Editor.TextProperty,"Description"); description.SetBinding(IsEnabledProperty,"IsNotBusy");
        var files = new VerticalStackLayout(); files.SetBinding(BindableLayout.ItemsSourceProperty,"Attachments"); BindableLayout.SetItemTemplate(files,new DataTemplate(() => UserViews.Card(UserViews.Stack(UserViews.Bound("FileName"),UserViews.Button("QUITAR","RemoveCommand",vm,".")))));
        var association = UserViews.Stack(UserViews.Input(UserViews.Entry("Identification","Identificación del usuario (opcional)")),UserViews.Button("ASOCIAR USUARIO","AssociateCommand"),GuardViews.Picker("RelatedVehicles","RelatedVehicle","Identifier","Vehículo asociado (opcional)"),UserViews.Button("QUITAR ASOCIACIÓN","ClearAssociationCommand")); association.SetBinding(IsVisibleProperty,"CanAssociate");
        var save = UserViews.Button("REGISTRAR INCIDENTE","SaveCommand"); save.SetBinding(IsEnabledProperty,"CanSave");
        var pick = UserViews.Button("ADJUNTAR ARCHIVO","PickCommand"); pick.SetBinding(IsEnabledProperty,"CanSave");
        Form(GuardViews.Lot(),UserViews.Bound("ContextLabel",true),GuardViews.Picker("Types","SelectedType"),UserViews.Input(description),UserViews.Bound("AssociationLabel"),association,
            new HorizontalStackLayout { Children = { occurred,UserViews.Text("Indicar fecha y hora de ocurrencia (Bogotá)") } },date,time,files,pick,save,UserViews.Button("VER INCIDENTES","ListCommand"));
    }
    public override void ApplyQueryAttributes(IDictionary<string,object> query) { if (query.TryGetValue("context",out var value) && value is IncidentContext context) ViewModel.Context=context; }
}
public sealed class IncidentDetailPage : UserPage<IncidentDetailViewModel>
{
    public IncidentDetailPage(IncidentDetailViewModel vm) : base(vm,"Detalle de incidente",()=>vm.LoadCommand.ExecuteAsync(null))
    {
        var files = new VerticalStackLayout(); files.SetBinding(BindableLayout.ItemsSourceProperty,"Detail.Attachments"); BindableLayout.SetItemTemplate(files,new DataTemplate(() => { var button=UserViews.Button("ABRIR ADJUNTO","OpenCommand",vm,"."); button.SetBinding(Button.TextProperty,"OriginalFileName"); return button; }));
        Form(UserViews.Bound("Summary"),files,UserViews.Button("ACTUALIZAR","LoadCommand"));
    }
    public override void ApplyQueryAttributes(IDictionary<string,object> query) { if (query.TryGetValue("incidentId",out var value) && Guid.TryParse(value.ToString(),out var id)) ViewModel.IncidentId=id; }
}
public static class GuardRoutes
{
    public static void Register(IServiceProvider services)
    {
        foreach (var route in new[] { ("guard-scan",typeof(ScanCardPage)),("guard-manual",typeof(ManualSearchPage)),("guard-access",typeof(AccessResultPage)),("guard-check-in",typeof(CheckInPage)),("guard-check-out",typeof(CheckOutPage)),("guard-inside",typeof(VehiclesInsidePage)),("guard-incidents",typeof(IncidentsPage)),("guard-create-incident",typeof(CreateIncidentPage)),("guard-incident-detail",typeof(IncidentDetailPage)),("guard-history",typeof(ParkingHistoryPage)) }) Routing.RegisterRoute(route.Item1,new Factory(services,route.Item2));
    }
    private sealed class Factory(IServiceProvider services,Type type) : RouteFactory
    { public override Element GetOrCreate() => (Element)services.GetRequiredService(type); public override Element GetOrCreate(IServiceProvider provider) => (Element)provider.GetRequiredService(type); }
}
