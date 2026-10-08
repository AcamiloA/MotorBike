using Microsoft.Extensions.DependencyInjection;
using UniversityParking.Mobile.Core;
using UniversityParking.Mobile.ViewModels;
using UniversityParking.Mobile.Pages.User;
using UniversityParking.Mobile.Pages.Guard;
using UniversityParking.Contracts.Users;
using UniversityParking.Contracts.Vehicles;
using UniversityParking.Contracts.AcademicPeriods;
using UniversityParking.Contracts.ParkingLots;
using UniversityParking.Contracts.News;

namespace UniversityParking.Mobile.Pages.Admin;

internal static class AdminViews
{
    public static View Field(string path,string label,bool password=false)=>UserViews.Input(UserViews.Entry(path,label,password));
    public static Editor Editor(string path,string label){var editor=new Editor{Placeholder=label,AutoSize=EditorAutoSizeOption.TextChanges,MinimumHeightRequest=120};editor.SetBinding(Microsoft.Maui.Controls.Editor.TextProperty,path);editor.SetBinding(VisualElement.IsEnabledProperty,"CanSave");return editor;}
    public static Button Save(string text,string command){var button=UserViews.Button(text,command);button.SetBinding(VisualElement.IsEnabledProperty,"CanSave");return button;}
    public static View Check(string path,string label){var check=new CheckBox();check.SetBinding(CheckBox.IsCheckedProperty,path);check.SetBinding(VisualElement.IsEnabledProperty,"CanSave");return new HorizontalStackLayout{Children={check,UserViews.Text(label)}};}
    public static View Lot()=>UserViews.Input(GuardViews.Picker("Lots","SelectedLot","Name","Parqueadero"));
    public static View Rows(object vm,string command="OpenRowCommand")=>UserViews.List("Items",new DataTemplate(()=>
    {var open=UserViews.Button("VER DETALLE",command,vm,".");open.SetBinding(Button.TextProperty,"Title");return UserViews.Card(UserViews.Stack(open,UserViews.Bound("Summary",true)));}));
    public static View ReadRows()=>UserViews.List("Items",new DataTemplate(()=>UserViews.Card(UserViews.Stack(UserViews.Bound("Title"),UserViews.Bound("Summary",true)))));
    public static Guid Id(IDictionary<string,object> query,string key)=>query.TryGetValue(key,out var value)&&Guid.TryParse(value.ToString(),out var id)?id:Guid.Empty;
}
public sealed class AdminDashboardPage:UserPage<AdminDashboardViewModel>
{
    public AdminDashboardPage(AdminDashboardViewModel vm):base(vm,"Administración",()=>vm.LoadCommand.ExecuteAsync(null))
    {
        var controls=new List<View>{UserViews.Bound("Summary"),UserViews.Button("ACTUALIZAR","LoadCommand")};
        foreach(var item in new[]{("USUARIOS","admin-users"),("VEHÍCULOS","admin-vehicles"),("PERIODOS ACADÉMICOS","admin-periods"),("PARQUEADEROS","admin-lots"),("INCIDENTES","admin-incidents"),("NOTICIAS","admin-news"),("HISTORIAL","admin-history"),("REPORTES","admin-reports"),("AUDITORÍA","admin-audit"),("MI CUENTA","admin-account")})
        {var button=UserViews.Button(item.Item1,"OpenCommand");button.CommandParameter=item.Item2;controls.Add(button);}
        var guard=UserViews.Button("CONTROL DE ACCESO GUARD","GuardCommand");guard.SetBinding(IsVisibleProperty,"HasGuard");controls.Add(guard);controls.Add(UserViews.Button("CERRAR SESIÓN","LogoutCommand"));Form(controls.ToArray());
    }
}
public abstract class AdminListPage<T>:UserPage<T> where T:AdminListViewModel
{
    protected AdminListPage(T vm,string title):base(vm,title,vm.LoadAsync){}
    protected void List(params View[] filters)
    {var scroll=new ScrollView{HeightRequest=250,Content=UserViews.Stack(filters)};Layout(GuardViews.ListLayout(scroll,AdminViews.Rows(ViewModel),ViewModel));}
    protected static View Type()=>GuardViews.Picker("Types","SelectedType");
    protected static View State()=>GuardViews.Picker("States","SelectedStatus");
    protected static View Filter()=>UserViews.Button("FILTRAR / ACTUALIZAR","RefreshCommand");
}
public sealed class AdminUsersPage:AdminListPage<AdminUsersViewModel>
{public AdminUsersPage(AdminUsersViewModel vm):base(vm,"Usuarios")=>List(AdminViews.Field("Search","Buscar por identificación o nombre"),Type(),State(),GuardViews.Picker("Roles","SelectedRole"),Filter(),UserViews.Button("CREAR USUARIO","CreateCommand"));}
public sealed class AdminVehiclesPage:AdminListPage<AdminVehiclesViewModel>
{public AdminVehiclesPage(AdminVehiclesViewModel vm):base(vm,"Vehículos")=>List(AdminViews.Field("Search","Buscar vehículo"),Type(),State(),AdminViews.Field("OwnerIdentification","Identificación del propietario"),GuardViews.Picker("Registrations","SelectedRegistration"),Filter());}
public sealed class AdminPeriodsPage:AdminListPage<AdminPeriodsViewModel>
{public AdminPeriodsPage(AdminPeriodsViewModel vm):base(vm,"Periodos académicos")=>List(AdminViews.Field("Search","Nombre del periodo"),State(),Filter(),UserViews.Button("CREAR PERIODO","CreateCommand"));}
public sealed class AdminLotsPage:AdminListPage<AdminLotsViewModel>
{public AdminLotsPage(AdminLotsViewModel vm):base(vm,"Parqueaderos")=>List(State(),Filter(),UserViews.Button("CREAR PARQUEADERO","CreateCommand"));}
public sealed class AdminIncidentsPage:AdminListPage<AdminIncidentsViewModel>
{public AdminIncidentsPage(AdminIncidentsViewModel vm):base(vm,"Incidentes — administración")=>List(AdminViews.Lot(),Type(),State(),GuardViews.Dates(),AdminViews.Field("Identification","Identificación del usuario (opcional)"),AdminViews.Field("VehicleIdentifier","Placa o número de marco (opcional)"),Filter(),UserViews.Button("REGISTRAR INCIDENTE","CreateCommand"));}
public sealed class AdminNewsPage:AdminListPage<AdminNewsViewModel>
{public AdminNewsPage(AdminNewsViewModel vm):base(vm,"Administrar noticias")=>List(AdminViews.Field("Search","Buscar noticia"),State(),Filter(),UserViews.Button("CREAR NOTICIA","CreateCommand"));}
public sealed class AdminHistoryPage:AdminListPage<AdminHistoryViewModel>
{public AdminHistoryPage(AdminHistoryViewModel vm):base(vm,"Historial general")=>List(AdminViews.Lot(),Type(),State(),GuardViews.Dates(),AdminViews.Field("Identification","Identificación del usuario"),AdminViews.Field("Plate","Placa"),AdminViews.Field("Frame","Número de marco"),Filter());}
public sealed class AdminAuditPage:AdminListPage<AdminAuditViewModel>
{public AdminAuditPage(AdminAuditViewModel vm):base(vm,"Auditoría")=>List(GuardViews.Dates(),AdminViews.Field("ActorIdentification","Identificación del actor (opcional)"),AdminViews.Field("Action","Acción"),AdminViews.Field("EntityType","Tipo de entidad"),AdminViews.Field("EntityId","Identificador de entidad (opcional)"),Filter());}
public sealed class AdminUserFormPage:UserPage<AdminUserFormViewModel>
{
    public AdminUserFormPage(AdminUserFormViewModel vm):base(vm,"Crear / editar usuario",()=>vm.LoadCommand.ExecuteAsync(null),once:true)
    {
        var identification=AdminViews.Field("Identification","Número de identificación");identification.SetBinding(IsVisibleProperty,"IsNew");
        var existing=UserViews.Bound("Identification");existing.SetBinding(IsVisibleProperty,new Binding("IsNew",converter:new InverseBooleanConverter()));
        var newOnly=UserViews.Stack(AdminViews.Field("InitialPassword","Contraseña inicial",true),UserViews.Text("8 caracteres, mayúscula, minúscula y número. USER es obligatorio."),AdminViews.Check("AddGuard","Agregar GUARD"),AdminViews.Check("AddAdmin","Agregar ADMIN"));newOnly.SetBinding(IsVisibleProperty,"IsNew");
        var universities=GuardViews.Picker("Universities","SelectedUniversity","Name","Seleccionar universidad");
        universities.SetBinding(IsEnabledProperty,"CanSelectUniversity");SemanticProperties.SetDescription(universities,"Universidad");
        Form(identification,existing,AdminViews.Field("FullName","Nombre completo"),UserViews.Text("Universidad",true),UserViews.Input(universities),UserViews.Bound("CurrentUniversityNotice",true),UserViews.Button("ACTUALIZAR / REINTENTAR UNIVERSIDADES","LoadCommand"),GuardViews.Picker("Members","Member"),UserViews.Bound("CareerLabel"),AdminViews.Field("Career","Carrera"),AdminViews.Field("CardCode","Código de carné"),newOnly,AdminViews.Save("GUARDAR USUARIO","SaveCommand"),UserViews.Button("VOLVER A USUARIOS","ListCommand"));
    }
    public override void ApplyQueryAttributes(IDictionary<string,object> query)=>ViewModel.UserId=AdminViews.Id(query,"userId");
    protected override void OnDisappearing(){ViewModel.InitialPassword="";base.OnDisappearing();}
}
public sealed class InverseBooleanConverter:IValueConverter
{public object Convert(object? value,Type targetType,object? parameter,System.Globalization.CultureInfo culture)=>value is not true;public object ConvertBack(object? value,Type targetType,object? parameter,System.Globalization.CultureInfo culture)=>value is not true;}
public sealed class AdminUserDetailPage:UserPage<AdminUserDetailViewModel>
{
    public AdminUserDetailPage(AdminUserDetailViewModel vm):base(vm,"Detalle de usuario",vm.LoadAsync)
    {
        var status=UserViews.Button("CAMBIAR ESTADO","StatusCommand");status.SetBinding(Button.TextProperty,"StatusAction");
        var header=new ScrollView{HeightRequest=280,Content=UserViews.Stack(UserViews.Bound("Summary"),UserViews.Button("EDITAR","EditCommand"),status,GuardViews.Picker("Roles","SelectedRole"),UserViews.Button("ASIGNAR ROL","AssignRoleCommand"),UserViews.Button("RETIRAR ROL","RemoveRoleCommand"),UserViews.Text("USER es obligatorio y no se puede retirar."),UserViews.Button("HISTORIAL DEL USUARIO","HistoryCommand"),UserViews.Button("ACTUALIZAR","RefreshCommand"),UserViews.Heading("Vehículos actuales"))};
        Layout(GuardViews.ListLayout(header,AdminViews.Rows(vm,"OpenVehicleCommand"),vm));
    }
    public override void ApplyQueryAttributes(IDictionary<string,object> query)=>ViewModel.UserId=AdminViews.Id(query,"userId");
}
public sealed class AdminVehicleDetailPage:UserPage<AdminVehicleDetailViewModel>
{
    public AdminVehicleDetailPage(AdminVehicleDetailViewModel vm):base(vm,"Detalle administrativo del vehículo",()=>vm.LoadCommand.ExecuteAsync(null))
    {
        var documents=new VerticalStackLayout();documents.SetBinding(BindableLayout.ItemsSourceProperty,"Detail.Documents");BindableLayout.SetItemTemplate(documents,new DataTemplate(()=>{var button=UserViews.Button("ABRIR DOCUMENTO","DocumentCommand",vm,".");button.SetBinding(Button.TextProperty,"OriginalFileName");return button;}));
        var status=AdminViews.Save("CAMBIAR ESTADO","StatusCommand");status.SetBinding(Button.TextProperty,"StatusAction");
        Form(UserViews.Bound("Summary"),UserViews.Image("Photo",180),documents,UserViews.Button("EDITAR MARCA / MODELO / COLOR","EditCommand"),status,AdminViews.Save("CORREGIR IDENTIFICADOR","CorrectCommand"),AdminViews.Save("TRANSFERIR VEHÍCULO","TransferCommand"),UserViews.Button("PROPIEDADES, REGISTROS E HISTORIAL","HistoryCommand"),UserViews.Button("ACTUALIZAR","LoadCommand"));
    }
    public override void ApplyQueryAttributes(IDictionary<string,object> query)=>ViewModel.VehicleId=AdminViews.Id(query,"vehicleId");
}
public sealed class AdminTransferPage:UserPage<AdminTransferViewModel>
{
    public AdminTransferPage(AdminTransferViewModel vm):base(vm,"Transferir vehículo")=>Form(UserViews.Bound("Summary"),AdminViews.Field("Identification","Identificación del nuevo propietario"),AdminViews.Save("BUSCAR / VALIDAR PROPIETARIO","FindCommand"),UserViews.Bound("OwnerSummary"),UserViews.Input(AdminViews.Editor("Reason","Motivo")),AdminViews.Save("TRANSFERIR","SaveCommand"));
    public override void ApplyQueryAttributes(IDictionary<string,object> query){if(query.TryGetValue("vehicle",out var value)&&value is VehicleResponse vehicle)ViewModel.Vehicle=vehicle;}
}
public sealed class AdminCorrectPage:UserPage<AdminCorrectViewModel>
{
    public AdminCorrectPage(AdminCorrectViewModel vm):base(vm,"Corregir identificador")=>Form(UserViews.Bound("Summary"),AdminViews.Field("Identifier","Nuevo identificador"),UserViews.Input(AdminViews.Editor("Reason","Motivo de la corrección")),AdminViews.Save("CONFIRMAR CORRECCIÓN","SaveCommand"));
    public override void ApplyQueryAttributes(IDictionary<string,object> query){if(query.TryGetValue("vehicle",out var value)&&value is VehicleResponse vehicle)ViewModel.Vehicle=vehicle;}
}
public sealed class AdminPeriodFormPage:UserPage<AdminPeriodFormViewModel>
{
    public AdminPeriodFormPage(AdminPeriodFormViewModel vm):base(vm,"Periodo académico")
    {
        var create=UserViews.Stack(AdminViews.Field("Name","Nombre del periodo"),UserViews.Text("Inicio"),GuardViews.Date("StartsOn"),UserViews.Text("Fin"),GuardViews.Date("EndsOn"),AdminViews.Save("CREAR PERIODO","SaveCommand"));create.SetBinding(IsVisibleProperty,"IsNew");
        var activate=AdminViews.Save("ACTIVAR","ActivateCommand");activate.SetBinding(IsVisibleProperty,"CanActivate");var close=AdminViews.Save("CERRAR PERIODO","CloseCommand");close.SetBinding(IsVisibleProperty,"CanClose");
        Form(UserViews.Bound("Name"),UserViews.Bound("State"),create,activate,close,UserViews.Text("Un periodo cerrado no puede reactivarse."));
    }
    public override void ApplyQueryAttributes(IDictionary<string,object> query){if(query.TryGetValue("period",out var value)&&value is AcademicPeriodResponse period)ViewModel.Period=period;}
}
public sealed class AdminLotFormPage:UserPage<AdminLotFormViewModel>
{
    public AdminLotFormPage(AdminLotFormViewModel vm):base(vm,"Crear / editar parqueadero")
    {
        var opening=new TimePicker();opening.SetBinding(TimePicker.TimeProperty,"Opening");var closing=new TimePicker();closing.SetBinding(TimePicker.TimeProperty,"Closing");
        var status=AdminViews.Save("CAMBIAR ESTADO","StatusCommand");status.SetBinding(Button.TextProperty,"StatusAction");status.SetBinding(IsVisibleProperty,"IsExisting");
        Form(UserViews.Bound("State"),AdminViews.Field("Name","Nombre"),AdminViews.Field("Campus","Sede"),UserViews.Text("Apertura"),opening,UserViews.Text("Cierre"),closing,AdminViews.Save("GUARDAR PARQUEADERO","SaveCommand"),status);
    }
    public override void ApplyQueryAttributes(IDictionary<string,object> query){if(query.TryGetValue("lot",out var value)&&value is ParkingLotResponse lot)ViewModel.Lot=lot;}
}
public sealed class AdminNewsFormPage:UserPage<AdminNewsFormViewModel>
{
    public AdminNewsFormPage(AdminNewsFormViewModel vm):base(vm,"Crear / editar noticia")
    {
        var publish=AdminViews.Save("PUBLICAR","PublishCommand");publish.SetBinding(IsVisibleProperty,"CanPublish");var archive=AdminViews.Save("ARCHIVAR","ArchiveCommand");archive.SetBinding(IsVisibleProperty,"CanArchive");
        var save=AdminViews.Save("GUARDAR","SaveCommand");save.SetBinding(IsVisibleProperty,"CanEditNews");
        var fields=UserViews.Stack(AdminViews.Field("Title","Título"),UserViews.Input(AdminViews.Editor("Content","Contenido")));fields.SetBinding(IsEnabledProperty,"CanEditNews");
        Form(UserViews.Bound("State"),fields,save,publish,archive,UserViews.Text("Guardar no publica automáticamente. Guarda los cambios antes de publicar."));
    }
    public override void ApplyQueryAttributes(IDictionary<string,object> query){if(query.TryGetValue("news",out var value)&&value is NewsResponse news)ViewModel.News=news;}
}
public sealed class AdminIncidentDetailPage:UserPage<AdminIncidentDetailViewModel>
{
    public AdminIncidentDetailPage(AdminIncidentDetailViewModel vm):base(vm,"Detalle administrativo del incidente",()=>vm.LoadCommand.ExecuteAsync(null))
    {
        var files=new VerticalStackLayout();files.SetBinding(BindableLayout.ItemsSourceProperty,"Detail.Attachments");BindableLayout.SetItemTemplate(files,new DataTemplate(()=>{var button=UserViews.Button("ABRIR ADJUNTO","OpenCommand",vm,".");button.SetBinding(Button.TextProperty,"OriginalFileName");return button;}));
        var resolve=UserViews.Button("RESOLVER INCIDENTE","ResolveFormCommand");var cancel=UserViews.Stack(AdminViews.Field("CancelReason","Motivo de cancelación (opcional)"),AdminViews.Save("CANCELAR INCIDENTE","CancelCommand"));resolve.SetBinding(IsVisibleProperty,"IsOpen");cancel.SetBinding(IsVisibleProperty,"IsOpen");
        Form(UserViews.Bound("Summary"),files,resolve,cancel,UserViews.Button("ACTUALIZAR","LoadCommand"));
    }
    public override void ApplyQueryAttributes(IDictionary<string,object> query)=>ViewModel.IncidentId=AdminViews.Id(query,"incidentId");
}
public sealed class AdminResolveIncidentPage:UserPage<AdminIncidentDetailViewModel>
{
    public AdminResolveIncidentPage(AdminIncidentDetailViewModel vm):base(vm,"Resolver incidente",()=>vm.LoadCommand.ExecuteAsync(null))=>Form(UserViews.Bound("Summary"),UserViews.Input(AdminViews.Editor("Resolution","Descripción obligatoria de la resolución")),AdminViews.Save("CONFIRMAR RESOLUCIÓN","ResolveCommand"),UserViews.Button("ACTUALIZAR ESTADO","LoadCommand"));
    public override void ApplyQueryAttributes(IDictionary<string,object> query)=>ViewModel.IncidentId=AdminViews.Id(query,"incidentId");
}
public sealed class AdminIncidentCreatePage:UserPage<AdminIncidentCreateViewModel>
{
    public AdminIncidentCreatePage(AdminIncidentCreateViewModel vm):base(vm,"Registrar incidente — administración",()=>vm.LoadCommand.ExecuteAsync(null),once:true)
    {
        var files=new VerticalStackLayout();files.SetBinding(BindableLayout.ItemsSourceProperty,"Attachments");BindableLayout.SetItemTemplate(files,new DataTemplate(()=>UserViews.Card(UserViews.Stack(UserViews.Bound("FileName"),UserViews.Button("QUITAR","RemoveCommand",vm,".")))));
        var date=GuardViews.Date("OccurredDate");date.SetBinding(IsVisibleProperty,"HasOccurredAt");var time=new TimePicker();time.SetBinding(TimePicker.TimeProperty,"OccurredTime");time.SetBinding(IsVisibleProperty,"HasOccurredAt");
        Form(AdminViews.Lot(),GuardViews.Picker("Types","SelectedType"),UserViews.Input(AdminViews.Editor("Description","Descripción")),AdminViews.Field("Identification","Identificación del usuario (opcional)"),AdminViews.Field("Identifier","Placa / número de marco (opcional)"),AdminViews.Check("HasOccurredAt","Indicar fecha / hora en Bogotá"),date,time,files,AdminViews.Save("ADJUNTAR ARCHIVO","PickCommand"),AdminViews.Save("REGISTRAR INCIDENTE","SaveCommand"),UserViews.Button("VER INCIDENTES","ListCommand"));
    }
    public override void ApplyQueryAttributes(IDictionary<string,object> query){if(query.TryGetValue("context",out var value)&&value is IncidentContext context)ViewModel.Context=context;}
}
public sealed class AdminReportsPage:UserPage<AdminReportsViewModel>
{
    public AdminReportsPage(AdminReportsViewModel vm):base(vm,"Reportes",()=>vm.InitializeCommand.ExecuteAsync(null))
    {
        var identification=AdminViews.Field("Identification","Identificación del usuario / celador");identification.SetBinding(IsVisibleProperty,"NeedsUser");var identifier=AdminViews.Field("Identifier","Placa o número de marco");identifier.SetBinding(IsVisibleProperty,"NeedsVehicle");var dates=GuardViews.Dates();dates.SetBinding(IsVisibleProperty,"UsesDates");
        var filters=new ScrollView{HeightRequest=250,Content=UserViews.Stack(GuardViews.Picker("Reports","SelectedReport"),AdminViews.Lot(),dates,identification,identifier,UserViews.Button("CONSULTAR REPORTE","RefreshCommand"))};
        Layout(GuardViews.ListLayout(filters,AdminViews.ReadRows(),vm));
    }
    public override void ApplyQueryAttributes(IDictionary<string,object> query)
    {
        if(AdminViews.Id(query,"userId") is{} user&&user!=Guid.Empty){ViewModel.SelectedReport=ViewModel.Reports.Single(x=>x.Code=="USER_HISTORY");ViewModel.Identification=query.TryGetValue("identification",out var identification)?identification.ToString()??"":"";ViewModel.TargetUserId=user;}
        if(AdminViews.Id(query,"vehicleId") is{} vehicle&&vehicle!=Guid.Empty){ViewModel.SelectedReport=ViewModel.Reports.Single(x=>x.Code=="VEHICLE_HISTORY");ViewModel.Identifier=query.TryGetValue("identifier",out var identifier)?identifier.ToString()??"":"";ViewModel.TargetVehicleId=vehicle;}
    }
}
public static class AdminRoutes
{
    public static void Register(IServiceProvider services)
    {
        Routing.RegisterRoute("admin-account",new Factory(services,typeof(ProfilePage)));
        foreach(var route in new[]{("admin-users",typeof(AdminUsersPage)),("admin-user-form",typeof(AdminUserFormPage)),("admin-user-detail",typeof(AdminUserDetailPage)),("admin-vehicles",typeof(AdminVehiclesPage)),("admin-vehicle-detail",typeof(AdminVehicleDetailPage)),("admin-transfer",typeof(AdminTransferPage)),("admin-correct",typeof(AdminCorrectPage)),("admin-periods",typeof(AdminPeriodsPage)),("admin-period-form",typeof(AdminPeriodFormPage)),("admin-lots",typeof(AdminLotsPage)),("admin-lot-form",typeof(AdminLotFormPage)),("admin-incidents",typeof(AdminIncidentsPage)),("admin-incident-detail",typeof(AdminIncidentDetailPage)),("admin-incident-resolve",typeof(AdminResolveIncidentPage)),("admin-incident-create",typeof(AdminIncidentCreatePage)),("admin-news",typeof(AdminNewsPage)),("admin-news-form",typeof(AdminNewsFormPage)),("admin-history",typeof(AdminHistoryPage)),("admin-reports",typeof(AdminReportsPage)),("admin-audit",typeof(AdminAuditPage))})Routing.RegisterRoute(route.Item1,new Factory(services,route.Item2));
    }
    private sealed class Factory(IServiceProvider services,Type type):RouteFactory
    {public override Element GetOrCreate()=>(Element)services.GetRequiredService(type);public override Element GetOrCreate(IServiceProvider provider)=>(Element)provider.GetRequiredService(type);}
}
