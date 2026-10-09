using UniversityParking.Contracts.Parking;
using UniversityParking.Mobile.Core;
using UniversityParking.Mobile.Pages.User;
using UniversityParking.Mobile.ViewModels;
using ZXing.Net.Maui;
using ZXing.Net.Maui.Controls;

namespace UniversityParking.Mobile.Pages.Guard;

public sealed class GuardAccessControlPage : ContentPage, IQueryAttributable
{
    private readonly GuardAccessControlViewModel vm;
    private readonly Grid cameraHolder = new();
    private readonly Image zoomImage = new() { Aspect = Aspect.AspectFit, AnchorX = .5, AnchorY = .5 };
    private readonly EvidenceViewport viewport = new();
    private CameraBarcodeReaderView? camera;
    private Window? subscribedWindow;
    private ParkingMovementResponse? pendingMovement;
    private bool active, windowRunning;
    private int scanClaimed;
    private double panX, panY;

    public GuardAccessControlPage(GuardAccessControlViewModel vm)
    {
        this.vm = vm; BindingContext = vm; Title = "Registro de acceso";
        SetDynamicResource(BackgroundColorProperty, "Background");
        var root = new Grid { RowDefinitions = new RowDefinitionCollection { new(GridLength.Auto), new(GridLength.Star), new(GridLength.Auto) } };
        root.Add(UserViews.Stack(UserViews.Heading(Title), GuardViews.Lot()), 0, 0);
        var scanner = new Grid(); scanner.Add(cameraHolder);
        var hint = UserViews.Text("Escanea el código QR del carné"); hint.VerticalOptions = LayoutOptions.End; hint.HorizontalTextAlignment = TextAlignment.Center;
        hint.BackgroundColor = Colors.Black; hint.TextColor = Colors.White; hint.Padding = 12; scanner.Add(hint);
        var body = new Grid(); body.Add(scanner); root.Add(body, 0, 1);

        var manual = UserViews.Stack(UserViews.Heading("Buscar por identificación"), UserViews.Input(UserViews.Entry("IdentificationNumber", "Número de identificación")), UserViews.Button("BUSCAR", "SearchCommand"));
        Overlay(body, manual, "ShowManual");
        var selection = new VerticalStackLayout { Spacing = 12 }; selection.SetBinding(BindableLayout.ItemsSourceProperty, "Vehicles");
        BindableLayout.SetItemTemplate(selection, new DataTemplate(() =>
        {
            var image = new Image { HeightRequest = 180, Aspect = Aspect.AspectFit }; image.SetBinding(Image.SourceProperty, new Binding("Image", converter: new BytesImageConverter()));
            return UserViews.Card(UserViews.Stack(UserViews.Bound("Heading"), UserViews.Bound("Description"), image, UserViews.Button("SELECCIONAR", "SelectCommand", vm, ".")));
        }));
        Overlay(body, UserViews.Stack(UserViews.Heading("Selecciona el vehículo que está ingresando"), selection), "ShowSelection");

        var evidence = new Image { HeightRequest = 260, Aspect = Aspect.AspectFit };
        evidence.SetBinding(Image.SourceProperty, new Binding("Evidence", converter: new BytesImageConverter()));
        byte[]? renderingBytes = null;
        evidence.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName != nameof(Image.IsLoading)) return;
            if (evidence.IsLoading) { renderingBytes = vm.Evidence; return; }
            var expected = renderingBytes;
            MainThread.BeginInvokeOnMainThread(() =>
            {
                var displayed = evidence.Handler?.PlatformView is Android.Widget.ImageView native && native.Drawable is { IntrinsicWidth: > 0, IntrinsicHeight: > 0 };
                vm.ReportEvidenceRendered(expected, displayed);
            });
        };
        var tap = new TapGestureRecognizer(); tap.SetBinding(TapGestureRecognizer.CommandProperty, "OpenZoomCommand"); evidence.GestureRecognizers.Add(tap);
        var enter = UserViews.Button("✓ REGISTRAR INGRESO", "ConfirmCommand"); enter.SetBinding(IsVisibleProperty, "ShowEntry"); enter.SetBinding(IsEnabledProperty, "CanConfirm");
        var exit = UserViews.Button("✓ REGISTRAR SALIDA", "ConfirmCommand"); exit.SetBinding(IsVisibleProperty, "ShowExit"); exit.SetBinding(IsEnabledProperty, "CanConfirm");
        Overlay(body, UserViews.Stack(UserViews.Bound("UserSummary"), UserViews.Bound("VehicleSummary"), UserViews.Bound("MovementSummary", true),
            UserViews.Bound("EvidenceLabel"), evidence, UserViews.Text("Toca para ampliar", true), UserViews.Bound("VisualInstruction", true),
            UserViews.Button("REINTENTAR IMAGEN", "ReloadEvidenceCommand"), enter, exit), "ShowConfirmation");
        Overlay(body, UserViews.Stack(UserViews.Bound("ResultMessage"), UserViews.Button("CONTINUAR", "ContinueCommand")), "ShowSuccess");

        var busy = new ActivityIndicator(); busy.SetBinding(ActivityIndicator.IsRunningProperty, "IsBusy"); busy.SetBinding(IsVisibleProperty, "IsBusy");
        var error = UserViews.Bound("ErrorMessage"); error.SetDynamicResource(Label.TextColorProperty, "Danger");
        var cancel = UserViews.Button("CANCELAR / VOLVER A ESCANEAR", "CancelCommand"); cancel.SetBinding(IsVisibleProperty, "ShowReset"); cancel.SetBinding(IsEnabledProperty, "CanReset");
        var verify = UserViews.Button("VERIFICAR ESTADO", "VerifyCommand"); verify.SetBinding(IsVisibleProperty, "NeedsVerification");
        var manualButton = UserViews.Button("REGISTRAR MANUALMENTE", "ManualCommand"); manualButton.SetBinding(IsEnabledProperty, "CanManual");
        root.Add(UserViews.Stack(busy, error, verify, cancel, manualButton), 0, 2);

        var zoom = new Grid { BackgroundColor = Colors.Black, RowDefinitions = new RowDefinitionCollection { new(GridLength.Auto), new(GridLength.Star) }, IsClippedToBounds = true };
        zoom.SetBinding(IsVisibleProperty, "ZoomOpen");
        zoomImage.SetBinding(Image.SourceProperty, new Binding("Evidence", converter: new BytesImageConverter()));
        var zoomArea = new Grid { IsClippedToBounds = true }; zoomArea.Add(zoomImage);
        zoom.Add(UserViews.Button("CERRAR IMAGEN", "CloseZoomCommand"), 0, 0); zoom.Add(zoomArea, 0, 1);
        var pinch = new PinchGestureRecognizer(); pinch.PinchUpdated += (_, e) =>
        {
            if (e.Status != GestureStatus.Running) return;
            viewport.Zoom(e.Scale, e.ScaleOrigin.X, e.ScaleOrigin.Y, zoomImage.Width, zoomImage.Height); ApplyViewport();
        };
        var pan = new PanGestureRecognizer(); pan.PanUpdated += (_, e) =>
        {
            if (e.StatusType == GestureStatus.Started) { panX = viewport.X; panY = viewport.Y; }
            if (e.StatusType == GestureStatus.Running) { viewport.Pan(panX + e.TotalX, panY + e.TotalY, zoomImage.Width, zoomImage.Height); ApplyViewport(); }
        };
        zoomImage.GestureRecognizers.Add(pinch); zoomImage.GestureRecognizers.Add(pan);
        root.Add(zoom, 0, 0); Grid.SetRowSpan(zoom, 3); Content = root;
        vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(vm.CameraMounted) or nameof(vm.IsDetecting)) MainThread.BeginInvokeOnMainThread(UpdateCamera);
            if (e.PropertyName == nameof(vm.ZoomOpen)) { viewport.Reset(); ApplyViewport(); }
        };
    }
    private static void Overlay(Grid body, View content, string visible)
    {
        var overlay = new ScrollView { Content = content, Padding = 16 }; overlay.SetDynamicResource(BackgroundColorProperty, "Background");
        overlay.SetBinding(IsVisibleProperty, visible); body.Add(overlay);
    }
    private void ApplyViewport() { zoomImage.Scale = viewport.Scale; zoomImage.TranslationX = viewport.X; zoomImage.TranslationY = viewport.Y; }
    private void UpdateCamera()
    {
        if (!active || !vm.CameraMounted) { ReleaseCamera(); return; }
        if (camera is null)
        {
            camera = new CameraBarcodeReaderView { CameraLocation = CameraLocation.Rear,
                Options = new BarcodeReaderOptions { Formats = BarcodeFormats.TwoDimensional, AutoRotate = true, Multiple = false } };
            camera.BarcodesDetected += Detected; cameraHolder.Add(camera);
        }
        camera.IsDetecting = vm.IsDetecting;
        if (vm.IsDetecting) Interlocked.Exchange(ref scanClaimed, 0);
    }
    private void Detected(object? sender, BarcodeDetectionEventArgs e)
    {
        var payload = e.Results.FirstOrDefault()?.Value;
        if (!active || string.IsNullOrWhiteSpace(payload) || Interlocked.CompareExchange(ref scanClaimed, 1, 0) != 0) return;
        MainThread.BeginInvokeOnMainThread(async () => { if (active) await vm.ScanAsync(payload); });
    }
    private void ReleaseCamera()
    {
        if (camera is null) return;
        camera.IsDetecting = false; camera.IsTorchOn = false; camera.BarcodesDetected -= Detected;
        cameraHolder.Children.Remove(camera); camera.Handler?.DisconnectHandler(); camera = null;
    }
    public void ApplyQueryAttributes(IDictionary<string, object> query)
    { if (query.TryGetValue("movement", out var value) && value is ParkingMovementResponse movement) pendingMovement = movement; }
    protected override async void OnAppearing()
    {
        base.OnAppearing(); active = windowRunning = true; subscribedWindow = Window;
        if (subscribedWindow is { } window) { window.Stopped += Stopped; window.Resumed += Resumed; }
        await InitializeAsync();
    }
    private async Task InitializeAsync()
    {
        await vm.StartCommand.ExecuteAsync(null);
        if (active && windowRunning && pendingMovement is { } movement) { pendingMovement = null; await vm.OpenMovementAsync(movement); }
        UpdateCamera();
    }
    private void Stopped(object? sender, EventArgs e) { windowRunning = false; vm.Stop(); ReleaseCamera(); }
    private async void Resumed(object? sender, EventArgs e)
    {
        windowRunning = true;
        // A suspended HTTP request may still be completing. Do not lose the resume request,
        // or remount a camera after another stop/departure while awaiting that operation.
        while (active && windowRunning && vm.IsBusy) await Task.Delay(50);
        if (active && windowRunning) await InitializeAsync();
    }
    protected override void OnDisappearing()
    {
        active = windowRunning = false; vm.Stop(); ReleaseCamera();
        if (subscribedWindow is { } window) { window.Stopped -= Stopped; window.Resumed -= Resumed; subscribedWindow = null; }
        base.OnDisappearing();
    }
}
