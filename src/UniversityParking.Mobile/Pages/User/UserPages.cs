using System.Globalization;
using Microsoft.Extensions.DependencyInjection;
using UniversityParking.Contracts.News;
using UniversityParking.Mobile.Core;
using UniversityParking.Mobile.ViewModels;

namespace UniversityParking.Mobile.Pages.User;

public sealed class BytesImageConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var bytes = value is PickedAttachment file ? file.Bytes : value as byte[];
        return bytes is { Length: > 0 } ? ImageSource.FromStream(() => new MemoryStream(bytes, writable: false)) : null;
    }
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotSupportedException();
}
public sealed class NonEmptyConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) => value is string text && !string.IsNullOrWhiteSpace(text);
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotSupportedException();
}
internal static class UserViews
{
    public static Label Text(string value, bool caption = false)
    { var label = new Label { Text = value }; if (caption) label.SetDynamicResource(Label.TextColorProperty, "TextSecondary"); return label; }
    public static Label Bound(string path, bool caption = false)
    { var label = Text("", caption); label.SetBinding(Label.TextProperty, path); return label; }
    public static Label Heading(string value) => new() { Text = value, FontSize = 21, FontFamily = "OpenSansSemibold" };
    public static Button Button(string text, string command, object? source = null, string? parameter = null)
    {
        var button = new Button { Text = text }; button.SetBinding(Microsoft.Maui.Controls.Button.CommandProperty, new Binding(command, source: source));
        button.SetBinding(VisualElement.IsEnabledProperty, new Binding("IsNotBusy", source: source));
        if (parameter is not null) button.SetBinding(Microsoft.Maui.Controls.Button.CommandParameterProperty, parameter); return button;
    }
    public static Entry Entry(string path, string placeholder, bool password = false, bool busyBinding = true)
    {
        var entry = new Entry { Placeholder = placeholder, IsPassword = password, IsTextPredictionEnabled = !password };
        entry.SetBinding(Microsoft.Maui.Controls.Entry.TextProperty, path); if (busyBinding) entry.SetBinding(VisualElement.IsEnabledProperty, "IsNotBusy");
        SemanticProperties.SetDescription(entry, placeholder); return entry;
    }
    public static Border Input(View control) { var border = new Border { Content = control }; border.SetDynamicResource(VisualElement.StyleProperty, "InputBorder"); return border; }
    public static Border Card(View content) { var border = new Border { Content = content }; border.SetDynamicResource(VisualElement.StyleProperty, "CardBorder"); return border; }
    public static Image Image(string path, double height)
    { var image = new Image { HeightRequest = height, Aspect = Aspect.AspectFit }; image.SetBinding(Microsoft.Maui.Controls.Image.SourceProperty, new Binding(path, converter: new BytesImageConverter())); return image; }
    public static VerticalStackLayout Stack(params View[] children)
    { var stack = new VerticalStackLayout { Spacing = 14 }; foreach (var child in children) stack.Children.Add(child); return stack; }
    public static View Pager(object vm)
    {
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitionCollection { new() { Width = GridLength.Star }, new() { Width = GridLength.Auto }, new() { Width = GridLength.Star } }, ColumnSpacing = 8 };
        var previous = Button("ANTERIOR", "PreviousCommand", vm); previous.SetBinding(VisualElement.IsEnabledProperty, new Binding("CanPrevious", source: vm));
        var next = Button("SIGUIENTE", "NextCommand", vm); next.SetBinding(VisualElement.IsEnabledProperty, new Binding("CanNext", source: vm));
        grid.Add(previous, 0, 0); var label = Bound("PageLabel", true); label.VerticalOptions = LayoutOptions.Center; grid.Add(label, 1, 0); grid.Add(next, 2, 0); return grid;
    }
    public static CollectionView List(string source, DataTemplate template)
    { var list = new CollectionView { ItemTemplate = template, SelectionMode = SelectionMode.None }; list.SetBinding(ItemsView.ItemsSourceProperty, source); return list; }
    public static RefreshView Refresh(View view, string command)
    { var refresh = new RefreshView { Content = view }; refresh.SetBinding(RefreshView.CommandProperty, command); refresh.SetBinding(RefreshView.IsRefreshingProperty, new Binding("IsBusy", BindingMode.OneWay)); return refresh; }
    public static View EmptyList(View list, string text)
    { var grid = new Grid(); grid.Children.Add(list); var empty = Text(text, true); empty.HorizontalTextAlignment = TextAlignment.Center; empty.VerticalOptions = LayoutOptions.Center; empty.SetBinding(VisualElement.IsVisibleProperty, "IsEmpty"); grid.Children.Add(empty); return grid; }
    public static DataTemplate MovementTemplate() => new(() => Card(Stack(Bound("Heading"), Bound("Entry", true), Bound("Exit", true), Bound("Summary", true), Bound("Status"))));
    public static DataTemplate NewsTemplate(object vm, string command = "OpenCommand") => new(() =>
    {
        var button = Button("", command, vm, "."); button.SetBinding(Microsoft.Maui.Controls.Button.TextProperty, "Title");
        return Card(Stack(button, Bound("Published", true), Bound("Preview", true)));
    });
    public static View Documents(object vm)
    {
        var container = new VerticalStackLayout { Spacing = 18 }; BindableLayout.SetItemsSource(container, null); container.SetBinding(BindableLayout.ItemsSourceProperty, "Documents");
        BindableLayout.SetItemTemplate(container, new DataTemplate(() =>
        {
            var pick = new Button { Text = "SELECCIONAR ARCHIVO" }; pick.SetBinding(Microsoft.Maui.Controls.Button.CommandProperty, "PickCommand"); pick.SetBinding(VisualElement.IsEnabledProperty, new Binding("IsNotBusy", source: vm));
            var issued = new CheckBox(); issued.SetBinding(CheckBox.IsCheckedProperty, "HasIssuedOn");
            var expires = new CheckBox(); expires.SetBinding(CheckBox.IsCheckedProperty, "HasExpiresOn");
            var dateIssued = new DatePicker(); dateIssued.SetBinding(DatePicker.DateProperty, "IssuedOn"); dateIssued.SetBinding(VisualElement.IsVisibleProperty, "HasIssuedOn");
            var dateExpires = new DatePicker(); dateExpires.SetBinding(DatePicker.DateProperty, "ExpiresOn"); dateExpires.SetBinding(VisualElement.IsVisibleProperty, "HasExpiresOn");
            var error = Bound("ErrorMessage"); error.SetDynamicResource(Label.TextColorProperty, "Danger");
            return Card(Stack(Bound("Label"), Bound("ExistingLabel", true), Bound("FileName", true), pick, Input(Entry("Number", "Número de documento (opcional)", busyBinding: false)),
                new HorizontalStackLayout { Children = { issued, Text("Indicar fecha de emisión", true) } }, dateIssued,
                new HorizontalStackLayout { Children = { expires, Text("Indicar vencimiento", true) } }, dateExpires, error));
        })); return container;
    }
}
public abstract class UserPage<T> : ContentPage, IQueryAttributable where T : class
{
    protected T ViewModel { get; }
    private readonly Func<Task>? load; private readonly bool once; private bool loaded;
    protected UserPage(T vm, string title, Func<Task>? load = null, bool once = false)
    { ViewModel = vm; BindingContext = vm; Title = title; this.load = load; this.once = once; SetDynamicResource(BackgroundColorProperty, "Background"); }
    protected void Layout(View body, View? footer = null)
    {
        var grid = new Grid { Padding = 20, RowSpacing = 14, RowDefinitions = new RowDefinitionCollection { new() { Height = GridLength.Auto }, new() { Height = GridLength.Star }, new() { Height = GridLength.Auto } } };
        var error = UserViews.Bound("ErrorMessage"); error.SetDynamicResource(Label.TextColorProperty, "Danger");
        var busy = new ActivityIndicator(); busy.SetBinding(ActivityIndicator.IsRunningProperty, "IsBusy"); busy.SetBinding(IsVisibleProperty, "IsBusy");
        grid.Add(UserViews.Stack(UserViews.Heading(Title), busy, error), 0, 0); grid.Add(body, 0, 1); if (footer is not null) grid.Add(footer, 0, 2); Content = grid;
    }
    protected void Form(params View[] children) => Layout(new ScrollView { Content = UserViews.Stack(children) });
    protected override async void OnAppearing() { base.OnAppearing(); if (load is null || once && loaded) return; loaded = true; await load(); }
    public virtual void ApplyQueryAttributes(IDictionary<string, object> query) { }
    protected static Guid VehicleId(IDictionary<string, object> query) => query.TryGetValue("vehicleId", out var value) && Guid.TryParse(value.ToString(), out var id) ? id : Guid.Empty;
}
public sealed class VehicleCardView : Border
{
    public VehicleCardView(object vm, string openCommand)
    {
        SetDynamicResource(StyleProperty, "CardBorder"); Margin = new Thickness(0, 0, 0, 12);
        var icon = new Image { HeightRequest = 30, WidthRequest = 40 }; icon.SetBinding(Image.SourceProperty, "TypeIcon");
        var open = UserViews.Button("VER DETALLE", openCommand, vm, "Id");
        var spinner = new ActivityIndicator(); spinner.SetBinding(ActivityIndicator.IsRunningProperty, "IsBusy"); spinner.SetBinding(IsVisibleProperty, "IsBusy");
        var photoError = UserViews.Bound("ErrorMessage", true); photoError.SetDynamicResource(Label.TextColorProperty, "Danger");
        var retryPhoto = UserViews.Button("REINTENTAR FOTO", "LoadPhotoCommand"); retryPhoto.SetBinding(IsVisibleProperty, new Binding("ErrorMessage", converter: new NonEmptyConverter()));
        Content = UserViews.Stack(new HorizontalStackLayout { Spacing = 10, Children = { icon, UserViews.Bound("TypeName") } }, UserViews.Bound("Identifier"),
            UserViews.Bound("Heading"), UserViews.Image("Photo", 130), spinner, photoError, retryPhoto, UserViews.Bound("Summary", true), UserViews.Bound("Registration", true), UserViews.Bound("Location"), open);
        BindingContextChanged += async (_, _) => { if (BindingContext is VehicleCardViewModel card) await card.LoadPhotoCommand.ExecuteAsync(null); };
    }
}
public sealed class UserHomePage : UserPage<UserHomeViewModel>
{
    public UserHomePage(UserHomeViewModel vm) : base(vm, "Inicio", () => vm.LoadCommand.ExecuteAsync(null))
    {
        var vehicles = new VerticalStackLayout(); vehicles.SetBinding(BindableLayout.ItemsSourceProperty, "Vehicles"); BindableLayout.SetItemTemplate(vehicles, new DataTemplate(() => new VehicleCardView(vm, "OpenVehicleCommand")));
        var news = new VerticalStackLayout { Spacing = 12 }; news.SetBinding(BindableLayout.ItemsSourceProperty, "RecentNews"); BindableLayout.SetItemTemplate(news, UserViews.NewsTemplate(vm, "OpenNewsCommand"));
        var history = new VerticalStackLayout { Spacing = 12 }; history.SetBinding(BindableLayout.ItemsSourceProperty, "RecentMovements"); BindableLayout.SetItemTemplate(history, UserViews.MovementTemplate());
        Form(UserViews.Bound("Greeting"), UserViews.Text("Tus accesos y vehículos universitarios", true), UserViews.Card(UserViews.Stack(UserViews.Text("Vehículos activos"), UserViews.Bound("ActiveCount"))),
            UserViews.Button("REGISTRAR VEHÍCULO", "RegisterCommand"), UserViews.Button("MIS VEHÍCULOS", "VehiclesCommand"), vehicles,
            UserViews.Button("VER HISTORIAL", "HistoryCommand"), history, UserViews.Button("NOTICIAS", "NewsCommand"), news, UserViews.Button("MI PERFIL", "ProfileCommand"), UserViews.Button("REINTENTAR", "LoadCommand"));
    }
}
public sealed class MyVehiclesPage : UserPage<MyVehiclesViewModel>
{
    public MyVehiclesPage(MyVehiclesViewModel vm) : base(vm, "Mis vehículos", () => vm.LoadCommand.ExecuteAsync(null)) =>
        Layout(UserViews.EmptyList(UserViews.Refresh(UserViews.List("Vehicles", new DataTemplate(() => new VehicleCardView(vm, "OpenCommand"))), "LoadCommand"), "No tienes vehículos registrados."),
            UserViews.Stack(UserViews.Button("REGISTRAR VEHÍCULO", "RegisterCommand"), UserViews.Button("REINTENTAR", "LoadCommand")));
}
public sealed class VehicleDetailPage : UserPage<VehicleDetailViewModel>
{
    public VehicleDetailPage(VehicleDetailViewModel vm) : base(vm, "Vehículo", () => vm.LoadCommand.ExecuteAsync(null))
    {
        var documents = new VerticalStackLayout { Spacing = 8 }; documents.SetBinding(BindableLayout.ItemsSourceProperty, "Documents");
        BindableLayout.SetItemTemplate(documents, new DataTemplate(() => { var button = UserViews.Button("", "OpenDocumentCommand", vm, "."); button.SetBinding(Button.TextProperty, "OriginalFileName"); return button; }));
        var edit = UserViews.Button("EDITAR INFORMACIÓN", "EditCommand"); edit.SetBinding(IsVisibleProperty, "IsOwner");
        var renew = UserViews.Button("RENOVAR REGISTRO", "RenewCommand"); renew.SetBinding(IsVisibleProperty, "CanRenew");
        var status = UserViews.Button("", "ChangeStatusCommand"); status.SetBinding(Button.TextProperty, "StatusAction"); status.SetBinding(IsVisibleProperty, "IsOwner");
        Form(UserViews.Image("Photo", 220), UserViews.Bound("Identifier"), UserViews.Bound("Heading"), UserViews.Bound("Summary", true), UserViews.Bound("Period", true),
            UserViews.Bound("Registration"), UserViews.Bound("Location"), UserViews.Heading("Documentos privados"), documents, edit, status, renew, UserViews.Button("REINTENTAR", "LoadCommand"));
    }
    public override void ApplyQueryAttributes(IDictionary<string, object> query) => ViewModel.VehicleId = VehicleId(query);
}
public sealed class RegisterVehiclePage : UserPage<RegisterVehicleViewModel>
{
    public RegisterVehiclePage(RegisterVehicleViewModel vm) : base(vm, "Registrar vehículo", () => vm.LoadCommand.ExecuteAsync(null), once: true)
    {
        var types = new Picker { Title = "Tipo de vehículo", ItemDisplayBinding = new Binding("Label") }; types.SetBinding(Picker.ItemsSourceProperty, "Types"); types.SetBinding(Picker.SelectedItemProperty, "SelectedType"); types.SetBinding(IsEnabledProperty, "IsNotBusy");
        var gallery = UserViews.Button("SELECCIONAR FOTO", "PickPhotoCommand"); gallery.CommandParameter = false;
        var camera = UserViews.Button("TOMAR FOTO", "PickPhotoCommand"); camera.CommandParameter = true;
        Form(UserViews.Input(types), UserViews.Bound("IdentifierLabel"), UserViews.Input(UserViews.Entry("Identifier", "Identificador del vehículo")),
            UserViews.Input(UserViews.Entry("Brand", "Marca")), UserViews.Input(UserViews.Entry("Model", "Modelo")), UserViews.Input(UserViews.Entry("Color", "Color")),
            UserViews.Heading("Fotografía GENERAL"), UserViews.Bound("PhotoName", true), UserViews.Image("Photo", 180), camera, gallery,
            UserViews.Heading("Documentos"), UserViews.Documents(vm), UserViews.Button("REGISTRAR VEHÍCULO", "SaveCommand"));
    }
}
public sealed class EditVehiclePage : UserPage<EditVehicleViewModel>
{
    public EditVehiclePage(EditVehicleViewModel vm) : base(vm, "Editar vehículo", () => vm.LoadCommand.ExecuteAsync(null), once: true) =>
        Form(UserViews.Bound("Identifier"), UserViews.Input(UserViews.Entry("Brand", "Marca")), UserViews.Input(UserViews.Entry("Model", "Modelo")), UserViews.Input(UserViews.Entry("Color", "Color")), UserViews.Button("GUARDAR CAMBIOS", "SaveCommand"));
    public override void ApplyQueryAttributes(IDictionary<string, object> query) => ViewModel.VehicleId = VehicleId(query);
}
public sealed class RenewRegistrationPage : UserPage<RenewRegistrationViewModel>
{
    public RenewRegistrationPage(RenewRegistrationViewModel vm) : base(vm, "Renovar registro", () => vm.LoadCommand.ExecuteAsync(null), once: true) =>
        Form(UserViews.Bound("Heading"), UserViews.Bound("Period", true), UserViews.Text("Puedes conservar los soportes actuales o adjuntar sus reemplazos.", true), UserViews.Documents(vm), UserViews.Button("RENOVAR REGISTRO", "SaveCommand"), UserViews.Button("REINTENTAR", "LoadCommand"));
    public override void ApplyQueryAttributes(IDictionary<string, object> query) => ViewModel.VehicleId = VehicleId(query);
}
public sealed class MyHistoryPage : UserPage<MyHistoryViewModel>
{
    public MyHistoryPage(MyHistoryViewModel vm) : base(vm, "Mi historial", () => vm.RefreshCommand.ExecuteAsync(null))
    {
        var from = new DatePicker(); from.SetBinding(DatePicker.DateProperty, "DateFrom"); var to = new DatePicker(); to.SetBinding(DatePicker.DateProperty, "DateTo");
        var vehicle = new Picker { Title = "Vehículo", ItemDisplayBinding = new Binding("Label") }; vehicle.SetBinding(Picker.ItemsSourceProperty, "Filters"); vehicle.SetBinding(Picker.SelectedItemProperty, "SelectedVehicle");
        var grid = new Grid { RowDefinitions = new RowDefinitionCollection { new() { Height = GridLength.Auto }, new() { Height = GridLength.Star } }, RowSpacing = 12 };
        grid.Add(UserViews.Stack(UserViews.Text("Fecha inicial / final (Bogotá)", true), new HorizontalStackLayout { Spacing = 16, Children = { from, to } }, vehicle, UserViews.Button("APLICAR FILTROS", "RefreshCommand")), 0, 0);
        grid.Add(UserViews.EmptyList(UserViews.Refresh(UserViews.List("Items", UserViews.MovementTemplate()), "RefreshCommand"), "No hay movimientos para los filtros seleccionados."), 0, 1); Layout(grid, UserViews.Pager(vm));
    }
}
public sealed class NewsPage : UserPage<NewsViewModel>
{
    public NewsPage(NewsViewModel vm) : base(vm, "Noticias", () => vm.RefreshCommand.ExecuteAsync(null)) =>
        Layout(UserViews.EmptyList(UserViews.Refresh(UserViews.List("Items", UserViews.NewsTemplate(vm)), "RefreshCommand"), "No hay noticias publicadas."), UserViews.Stack(UserViews.Button("REINTENTAR", "RefreshCommand"), UserViews.Pager(vm)));
}
public sealed class NewsDetailPage : UserPage<NewsDetailViewModel>
{
    public NewsDetailPage(NewsDetailViewModel vm) : base(vm, "Noticia") => Form(UserViews.Bound("Item.Title"), UserViews.Bound("Published", true), UserViews.Bound("Item.Content"));
    public override void ApplyQueryAttributes(IDictionary<string, object> query)
    { if (query.TryGetValue("news", out var news) && news is NewsResponse { Status: "PUBLISHED" } item) ViewModel.Item = item; }
}
public sealed class ProfilePage : UserPage<ProfileViewModel>
{
    public ProfilePage(ProfileViewModel vm) : base(vm, "Mi perfil", () => vm.LoadCommand.ExecuteAsync(null)) =>
        Form(UserViews.Text("Nombre", true), UserViews.Bound("Profile.FullName"), UserViews.Text("Identificación", true), UserViews.Bound("Profile.IdentificationNumber"),
            UserViews.Text("Universidad", true), UserViews.Bound("Profile.University"), UserViews.Text("Carrera", true), UserViews.Bound("Profile.Career"), UserViews.Bound("MemberType"), UserViews.Bound("Roles", true),
            UserViews.Text("Código de carné", true), UserViews.Bound("Profile.CardCode"), UserViews.Button("EDITAR PERFIL", "EditCommand"), UserViews.Button("CAMBIAR CONTRASEÑA", "PasswordCommand"), UserViews.Button("CERRAR SESIÓN", "LogoutCommand"), UserViews.Button("REINTENTAR", "LoadCommand"));
}
public sealed class EditProfilePage : UserPage<EditProfileViewModel>
{
    public EditProfilePage(EditProfileViewModel vm) : base(vm, "Editar perfil", () => vm.LoadCommand.ExecuteAsync(null), once: true) =>
        Form(UserViews.Input(UserViews.Entry("FullName", "Nombre completo")), UserViews.Input(UserViews.Entry("Career", "Carrera (opcional)")), UserViews.Button("GUARDAR PERFIL", "SaveCommand"));
}
public sealed class ChangePasswordPage : UserPage<ChangePasswordViewModel>
{
    public ChangePasswordPage(ChangePasswordViewModel vm) : base(vm, "Cambiar contraseña") =>
        Form(UserViews.Text("Mínimo 8 caracteres, mayúscula, minúscula y número.", true), UserViews.Input(UserViews.Entry("CurrentPassword", "Contraseña actual", password: true)),
            UserViews.Input(UserViews.Entry("NewPassword", "Nueva contraseña", password: true)), UserViews.Input(UserViews.Entry("Confirmation", "Confirmar nueva contraseña", password: true)), UserViews.Button("ACTUALIZAR CONTRASEÑA", "SaveCommand"));
}
public static class UserRoutes
{
    public static void Register(IServiceProvider services)
    {
        var routes = new Dictionary<string, Type> { ["my-vehicles"] = typeof(MyVehiclesPage), ["vehicle-detail"] = typeof(VehicleDetailPage), ["vehicle-register"] = typeof(RegisterVehiclePage),
            ["vehicle-edit"] = typeof(EditVehiclePage), ["vehicle-renew"] = typeof(RenewRegistrationPage), ["my-history"] = typeof(MyHistoryPage), ["user-news"] = typeof(NewsPage),
            ["news-detail"] = typeof(NewsDetailPage), ["user-profile"] = typeof(ProfilePage), ["profile-edit"] = typeof(EditProfilePage), ["password-change"] = typeof(ChangePasswordPage) };
        foreach (var route in routes) Routing.RegisterRoute(route.Key, new ServiceRouteFactory(services, route.Value));
    }
    private sealed class ServiceRouteFactory(IServiceProvider services, Type type) : RouteFactory
    {
        public override Element GetOrCreate() => (Element)services.GetRequiredService(type);
        public override Element GetOrCreate(IServiceProvider provider) => GetOrCreate();
    }
}
