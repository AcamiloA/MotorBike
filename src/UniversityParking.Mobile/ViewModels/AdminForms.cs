using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using UniversityParking.Mobile.Core;
using UniversityParking.Contracts.Users;
using UniversityParking.Contracts.Universities;
using UniversityParking.Contracts.Vehicles;
using UniversityParking.Contracts.AcademicPeriods;
using UniversityParking.Contracts.ParkingLots;
using UniversityParking.Contracts.Incidents;
using UniversityParking.Contracts.News;

namespace UniversityParking.Mobile.ViewModels;

public abstract partial class AdminFeatureViewModel(IAuthSession session):UserFeatureViewModel
{
    protected IAuthSession Session=>session;
    [ObservableProperty] private bool completed;
    [ObservableProperty] private bool writeUncertain;
    public bool CanSave=>!IsBusy&&!Completed&&!WriteUncertain;
    protected override void OnPropertyChanged(System.ComponentModel.PropertyChangedEventArgs e){base.OnPropertyChanged(e);if(e.PropertyName is nameof(IsBusy) or nameof(Completed) or nameof(WriteUncertain))base.OnPropertyChanged(new System.ComponentModel.PropertyChangedEventArgs(nameof(CanSave)));}
    protected void Require()=>AdminPresentation.RequireAdmin(session);
    protected bool Mutation<T>(ApiResult<T> result)
    {
        if(Accepted(result))return true;
        if(GuardPresentation.Uncertain(result.Error)){WriteUncertain=true;ErrorMessage="No se pudo confirmar el resultado. Consulta el estado actualizado antes de realizar otra operación.";}
        return false;
    }
}
public partial class AdminUserFormViewModel(AdminApiService api,IAuthSession session,IUserNavigation navigation):AdminFeatureViewModel(session)
{
    public Guid UserId {get;set;}
    public bool IsNew=>UserId==Guid.Empty;
    public IReadOnlyList<TypeChoice> Members {get;}=[new("STUDENT","ESTUDIANTE"),new("TEACHER","DOCENTE"),new("ADMINISTRATIVE","ADMINISTRATIVO"),new("GUARD","GUARDA")];
    [ObservableProperty] private TypeChoice member=new("STUDENT","ESTUDIANTE");
    [ObservableProperty] private string identification="";
    [ObservableProperty] private string fullName="";
    [ObservableProperty] private string firstName="";
    [ObservableProperty] private string lastName="";
    public IReadOnlyList<TypeChoice> IdentificationTypes {get;}=[new("CC","Cédula de ciudadanía"),new("CE","Cédula de extranjería"),new("TI","Tarjeta de identidad"),new("PASSPORT","Pasaporte")];
    [ObservableProperty] private TypeChoice identificationType=new("CC","Cédula de ciudadanía");
    public ObservableCollection<UniversityResponse> Universities {get;}=[];
    [ObservableProperty,NotifyPropertyChangedFor(nameof(UniversityId),nameof(UniversityName),nameof(CanSave),nameof(CurrentUniversityNotice))]
    private UniversityResponse? selectedUniversity;
    [ObservableProperty,NotifyPropertyChangedFor(nameof(CanSave),nameof(CanSelectUniversity))] private bool universitiesLoaded;
    private bool detailsLoaded;
    private Guid? currentInactiveId;
    public Guid UniversityId=>SelectedUniversity?.Id??Guid.Empty;
    public string UniversityName=>SelectedUniversity?.Name??"";
    public string CurrentUniversityNotice=>SelectedUniversity?.Id==currentInactiveId&&currentInactiveId.HasValue
        ?"La universidad actual no está en el catálogo activo. Puedes conservarla o elegir otra.":"";
    public bool RequiresUniversity=>Member.Code is "STUDENT" or "TEACHER";
    public new bool CanSave=>base.CanSave&&UniversitiesLoaded&&detailsLoaded&&(!RequiresUniversity || SelectedUniversity is not null&&Universities.Contains(SelectedUniversity));
    public bool CanSelectUniversity=>base.CanSave&&UniversitiesLoaded;
    [ObservableProperty] private string career="";
    [ObservableProperty] private string cardCode="";
    [ObservableProperty] private string email="";[ObservableProperty] private string phoneNumber="";[ObservableProperty] private string confirmPassword="";[ObservableProperty] private string initialPassword="";
    [ObservableProperty] private bool addGuard;
    [ObservableProperty] private bool addAdmin;
    public string CareerLabel=>Member?.Code=="STUDENT"?"Carrera (obligatoria)":"Carrera (opcional)";
    partial void OnMemberChanged(TypeChoice value){OnPropertyChanged(nameof(CareerLabel));OnPropertyChanged(nameof(RequiresUniversity));OnPropertyChanged(nameof(CanSave));}
    [RelayCommand] private Task LoadAsync()=>WorkAsync(async()=>
    {
        Require();var accountId=Session.User!.Id;var targetId=UserId;var previousId=SelectedUniversity?.Id;
        OnPropertyChanged(nameof(IsNew));UniversitiesLoaded=false;detailsLoaded=false;
        SelectedUniversity=null;Universities.Clear();currentInactiveId=null;OnPropertyChanged(nameof(CurrentUniversityNotice));
        var catalog=await api.UniversitiesAsync();if(!Accepted(catalog))return;
        if(Session.User?.Id!=accountId||UserId!=targetId)return;Require();
        var values=catalog.Value!;
        if(values.Any(x=>x.Id==Guid.Empty||string.IsNullOrWhiteSpace(x.Name))||values.Select(x=>x.Id).Distinct().Count()!=values.Length)
            throw new UserInputException("No fue posible cargar las universidades. Intenta nuevamente.");
        UserProfileResponse? user=null;
        if(!IsNew)
        {
            var result=await api.UserAsync(UserId);if(!Accepted(result))return;
            if(Session.User?.Id!=accountId||UserId!=targetId)return;Require();user=result.Value!;
            if(user.Id!=UserId||user.UniversityId==Guid.Empty||string.IsNullOrWhiteSpace(user.UniversityName))
                throw new UserInputException("No fue posible cargar la universidad del usuario.");
        }
        foreach(var value in values)Universities.Add(value);
        if(user is not null)
        {
            Identification=user.IdentificationNumber;FullName=user.FullName;Career=user.Career??"";CardCode=user.CardCode;
            Email=user.Email??"";PhoneNumber=user.PhoneNumber??"";Member=Members.FirstOrDefault(x=>x.Code==user.UserType)??throw new UserInputException("El tipo de miembro no es válido.");
            var current=Universities.FirstOrDefault(x=>x.Id==user.UniversityId);
            if(current is null){currentInactiveId=user.UniversityId;current=new(user.UniversityId,"",user.UniversityName);Universities.Add(current);}
            SelectedUniversity=current;
        }
        else if(previousId is { } id)SelectedUniversity=Universities.FirstOrDefault(x=>x.Id==id);
        detailsLoaded=true;UniversitiesLoaded=true;
        if(Universities.Count==0)ErrorMessage="No hay universidades activas disponibles.";
    });
    [RelayCommand] private Task SaveAsync()=>WorkAsync(async()=>
    {
        if(Completed||WriteUncertain)return;Require();
        if(!UniversitiesLoaded||!detailsLoaded)throw new UserInputException("Carga las universidades antes de guardar.");
        if(RequiresUniversity&&(SelectedUniversity is null||!Universities.Contains(SelectedUniversity)||UniversityId==Guid.Empty))throw new UserInputException("Selecciona una universidad.");
        if(IsNew){Required(FirstName,"Los nombres",100);Required(LastName,"Los apellidos",100);FullName=FirstName.Trim()+" "+LastName.Trim();}
        Required(FullName,"El nombre",200);Required(Email,"El correo",254);Required(PhoneNumber,"El teléfono",40);
        if(Member is null||!Members.Any(x=>x.Code==Member.Code))throw new UserInputException("Selecciona un tipo de miembro válido.");if(Member.Code=="STUDENT")Required(Career,"La carrera",200);else if(Career.Trim().Length>200)throw new UserInputException("La carrera admite hasta 200 caracteres.");
        var institutional=Enum.Parse<UserInstitutionalType>(Member.Code);var kind=Member.Code=="STUDENT"?UserMemberType.STUDENT:Member.Code=="TEACHER"?UserMemberType.TEACHER:UserMemberType.STAFF;var career=string.IsNullOrWhiteSpace(Career)?null:Career.Trim();
        if(IsNew)
        {
            Required(Identification,"La identificación",50);if(!PasswordPresentation.IsValid(InitialPassword))throw new UserInputException("La contraseña inicial requiere 8 caracteres, mayúscula, minúscula y número.");
            if(InitialPassword!=ConfirmPassword)throw new UserInputException("Las contraseñas no coinciden.");
            try {var result=await api.CreateUserAsync(new(Identification.Trim(),FullName.Trim(),UniversityId,career,kind,null,InitialPassword,null,institutional,Email.Trim(),PhoneNumber.Trim(),IdentificationType.Code));if(!Mutation(result))return;Completed=true;await navigation.GoAsync("admin-user-detail",new Dictionary<string,object>{["userId"]=result.Value!.Id});}
            finally{InitialPassword="";}
        }
        else {var result=await api.EditUserAsync(UserId,new(FullName.Trim(),UniversityId,career,kind,null,institutional,Email.Trim(),PhoneNumber.Trim()));if(Mutation(result)){Completed=true;await navigation.GoAsync("admin-user-detail",new Dictionary<string,object>{["userId"]=UserId});}}
    });
    [RelayCommand] private Task ListAsync()=>navigation.GoAsync("admin-users");
    protected override void OnPropertyChanged(System.ComponentModel.PropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if(e.PropertyName is nameof(IsBusy) or nameof(Completed) or nameof(WriteUncertain))
            base.OnPropertyChanged(new System.ComponentModel.PropertyChangedEventArgs(nameof(CanSelectUniversity)));
    }
}
public partial class AdminUserDetailViewModel(AdminApiService api,IAuthSession session,IUserNavigation navigation,IAppNavigation appNavigation):PagedUserViewModel<AdminRow>
{
    [ObservableProperty] private bool showPasswordReset;
    [ObservableProperty] private string temporaryPassword="";
    [ObservableProperty] private string confirmTemporaryPassword="";
    [RelayCommand] private void OpenPasswordReset()=>ShowPasswordReset=true;
    [RelayCommand] private Task ResetPasswordAsync()=>WorkAsync(async()=>
    {
        if(User is null||WriteUncertain)return;AdminPresentation.RequireAdmin(session);
        if(!PasswordPresentation.IsValid(TemporaryPassword)||TemporaryPassword!=ConfirmTemporaryPassword)throw new UserInputException("Verifica la contraseña temporal y su confirmación.");
        try{if(Mutation(await api.ResetPasswordAsync(UserId,TemporaryPassword))){ShowPasswordReset=false;await navigation.MessageAsync("Contraseña","Se solicitará cambiar la contraseña temporal al ingresar.");}}
        finally{TemporaryPassword=ConfirmTemporaryPassword="";}
    });
    public Guid UserId {get;set;}
    [ObservableProperty] private UserProfileResponse? user;
    [ObservableProperty] private TypeChoice selectedRole=new("GUARD","GUARD");
    [ObservableProperty] private bool writeUncertain;
    public IReadOnlyList<TypeChoice> Roles {get;}=[new("GUARD","GUARD"),new("ADMIN","ADMIN")];
    public string Summary=>User is null?"":$"{User.FullName}\n{User.IdentificationNumber} · {RoleNavigation.InstitutionalTitle(User.UserType)}\n{User.UniversityName} · {User.Career}\n{User.Email}\n{User.PhoneNumber}\n{AdminPresentation.Status(User.Status)}\nRoles: {string.Join(", ",User.Roles)}";
    public string StatusAction=>User?.Status=="ACTIVE"?"DESACTIVAR USUARIO":User?.Status=="INACTIVE"?"ACTIVAR USUARIO":"";
    public bool ShowOperationalStatus=>User?.Status is "ACTIVE" or "INACTIVE";
    public bool IsPendingRegistration=>User is {Status:"PENDING",MemberType:"STUDENT"};
    public bool CanReviewRegistration=>IsPendingRegistration&&!IsBusy&&!WriteUncertain;
    partial void OnUserChanged(UserProfileResponse? value){OnPropertyChanged(nameof(Summary));OnPropertyChanged(nameof(StatusAction));OnPropertyChanged(nameof(ShowOperationalStatus));OnPropertyChanged(nameof(IsPendingRegistration));OnPropertyChanged(nameof(CanReviewRegistration));}
    public Task LoadAsync()=>LoadPageAsync();
    protected override Task LoadPageAsync()=>WorkAsync(ReadAsync);
    private async Task ReadAsync()
    {
        AdminPresentation.RequireAdmin(session);var profile=await api.UserAsync(UserId);if(!Accepted(profile))return;User=profile.Value;WriteUncertain=false;
        var vehicles=await api.VehiclesAsync(null,null,null,User!.IdentificationNumber,null,Page);if(!Accepted(vehicles))return;var value=vehicles.Value!;Apply(new(value.Items.Select(AdminVehiclesViewModel.VehicleRow).ToArray(),value.Page,value.PageSize,value.TotalCount,value.TotalPages));
    }
    [RelayCommand] private Task EditAsync()=>WorkAsync(async()=>{AdminPresentation.RequireAdmin(session);await navigation.GoAsync("admin-user-form",new Dictionary<string,object>{["userId"]=UserId});});
    [RelayCommand] private Task OpenVehicleAsync(AdminRow? row)=>row?.Value is VehicleResponse vehicle?navigation.GoAsync("admin-vehicle-detail",new Dictionary<string,object>{["vehicleId"]=vehicle.Id}):Task.CompletedTask;
    [RelayCommand] private Task ApproveRegistrationAsync()=>ReviewAsync(true);
    [RelayCommand] private Task RejectRegistrationAsync()=>ReviewAsync(false);
    private Task ReviewAsync(bool approve)=>WorkAsync(async()=>
    {
        AdminPresentation.RequireAdmin(session);
        if(!IsPendingRegistration||WriteUncertain)return;
        var id=UserId;var accountId=session.User!.Id;
        if(!await navigation.ConfirmAsync(approve?"Aprobar registro":"Rechazar registro",
            $"{(approve?"¿Aprobar":"¿Rechazar")} el registro de {User!.FullName}?"))return;
        AdminPresentation.RequireAdmin(session);
        if(session.User?.Id!=accountId||UserId!=id)return;
        if(Mutation(await api.ReviewStudentAsync(id,approve)))await ReadAsync();
    });
    protected override void OnPropertyChanged(System.ComponentModel.PropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if(e.PropertyName is nameof(IsBusy) or nameof(WriteUncertain))
            base.OnPropertyChanged(new System.ComponentModel.PropertyChangedEventArgs(nameof(CanReviewRegistration)));
    }
    [RelayCommand] private Task HistoryAsync()=>navigation.GoAsync("admin-reports",new Dictionary<string,object>{["userId"]=UserId,["identification"]=User?.IdentificationNumber??""});
    private bool Mutation(ApiResult<bool> result){if(Accepted(result))return true;if(GuardPresentation.Uncertain(result.Error)){WriteUncertain=true;ErrorMessage="No se confirmó la operación. Actualiza el detalle antes de intentarlo de nuevo.";}return false;}
    [RelayCommand] private Task StatusAsync()=>WorkAsync(async()=>
    {if(User is null||WriteUncertain||!ShowOperationalStatus)return;AdminPresentation.RequireAdmin(session);var active=User.Status=="INACTIVE";if(!active&&!await navigation.ConfirmAsync("Desactivar usuario",$"¿Desactivar a {User.FullName}?"))return;if(Mutation(await api.UserStatusAsync(UserId,active))){if(!active&&session.User?.Id==UserId){await session.ClearAsync();await appNavigation.ShowLoginAsync("Tu cuenta fue desactivada.");}else await ReadAsync();}});
    [RelayCommand] private Task AssignRoleAsync()=>RoleAsync(false);
    [RelayCommand] private Task RemoveRoleAsync()=>RoleAsync(true);
    private Task RoleAsync(bool remove)=>WorkAsync(async()=>
    {
        if(User is null||WriteUncertain)return;AdminPresentation.RequireAdmin(session);if(SelectedRole is null||!Roles.Any(x=>x.Code==SelectedRole.Code))throw new UserInputException("USER es obligatorio y no se puede retirar.");
        if(!await navigation.ConfirmAsync(remove?"Retirar rol":"Asignar rol",$"¿{(remove?"Retirar":"Asignar")} {SelectedRole.Code} a {User.FullName}?"))return;
        var result=remove?await api.RemoveRoleAsync(UserId,SelectedRole.Code):await api.AssignRoleAsync(UserId,SelectedRole.Code);if(Mutation(result)){if(session.User?.Id==UserId){await session.ClearAsync();await appNavigation.ShowLoginAsync("Tus roles cambiaron. Inicia sesión nuevamente para actualizar tus permisos.");}else await ReadAsync();}
    });
}
public partial class AdminVehicleDetailViewModel(AdminApiService api,IAuthSession session,IUserNavigation navigation,IFileViewer viewer):AdminFeatureViewModel(session)
{
    public Guid VehicleId {get;set;}
    [ObservableProperty] private VehicleDetailResponse? detail;
    [ObservableProperty] private byte[]? photo;
    public string Summary=>Detail is null?"":AdminVehiclesViewModel.VehicleRow(Detail.Vehicle).Title+"\n"+AdminVehiclesViewModel.VehicleRow(Detail.Vehicle).Summary;
    public string StatusAction=>Detail?.Vehicle.Status=="ACTIVE"?"DESACTIVAR VEHÍCULO":"ACTIVAR VEHÍCULO";
    public string VerificationLabel=>VehiclePresentation.VerificationLabel(Detail?.Vehicle.Type);
    public string VerificationPending=>Detail?.VerificationImage is null?"Evidencia de verificación pendiente.":"";
    [RelayCommand] private Task UpdateVerificationAsync()=>navigation.GoAsync("vehicle-verification",new Dictionary<string,object>{["vehicleId"]=VehicleId});
    partial void OnDetailChanged(VehicleDetailResponse? value){OnPropertyChanged(nameof(Summary));OnPropertyChanged(nameof(StatusAction));}
    [RelayCommand] private Task LoadAsync()=>WorkAsync(ReadAsync);
    private async Task ReadAsync()
    {
        Require();Detail=null;Photo=null;var result=await api.VehicleAsync(VehicleId);if(!Accepted(result))return;Detail=result.Value;WriteUncertain=false;
        var path=Detail!.VerificationImage?.ContentUrl;OnPropertyChanged(nameof(VerificationLabel));OnPropertyChanged(nameof(VerificationPending));if(path is not null){var image=await api.FileAsync(path);if(Accepted(image))Photo=image.Value;}
    }
    [RelayCommand] private Task StatusAsync()=>WorkAsync(async()=>{if(Detail is null||WriteUncertain)return;Require();var active=Detail.Vehicle.Status!="ACTIVE";if(!active&&!await navigation.ConfirmAsync("Desactivar vehículo","¿Desactivar este vehículo?"))return;if(Mutation(await api.VehicleStatusAsync(VehicleId,active)))await ReadAsync();});
    [RelayCommand] private Task TransferAsync()=>OpenForm("admin-transfer");
    [RelayCommand] private Task CorrectAsync()=>OpenForm("admin-correct");
    private Task OpenForm(string route)=>WorkAsync(async()=>{Require();if(Detail is not null)await navigation.GoAsync(route,new Dictionary<string,object>{["vehicle"]=Detail.Vehicle});});
    [RelayCommand] private Task EditAsync()=>navigation.GoAsync("vehicle-edit",new Dictionary<string,object>{["vehicleId"]=VehicleId});
    [RelayCommand] private Task HistoryAsync()=>navigation.GoAsync("admin-reports",new Dictionary<string,object>{["vehicleId"]=VehicleId,["identifier"]=Detail is null?"":AdminPresentation.Identifier(Detail.Vehicle)});
    [RelayCommand] private Task DocumentAsync(VehicleDocumentResponse? file)=>WorkAsync(async()=>{if(file is null)return;Require();var token=await Session.GetTokenAsync();var result=await api.FileAsync(file.ContentUrl);if(Accepted(result)&&Session.User is not null&&token==await Session.GetTokenAsync())await viewer.OpenAsync(file.OriginalFileName,file.ContentType,result.Value!);});
}
public partial class AdminTransferViewModel(AdminApiService api,IAuthSession session,IUserNavigation navigation):AdminFeatureViewModel(session)
{
    [ObservableProperty] private VehicleResponse? vehicle;
    [ObservableProperty] private string identification="";
    [ObservableProperty] private string reason="";
    [ObservableProperty] private UserListItemResponse? newOwner;
    public string Summary=>Vehicle is null?"":AdminPresentation.Identifier(Vehicle)+" · Propietario actual: "+Vehicle.CurrentOwnerFullName;
    public string OwnerSummary=>NewOwner is null?"Busca y valida el nuevo propietario.":$"{NewOwner.FullName} · {NewOwner.IdentificationNumber}\n{AdminPresentation.Member(NewOwner.MemberType)} · {AdminPresentation.Status(NewOwner.Status)}";
    partial void OnVehicleChanged(VehicleResponse? value)=>OnPropertyChanged(nameof(Summary));
    partial void OnNewOwnerChanged(UserListItemResponse? value)=>OnPropertyChanged(nameof(OwnerSummary));
    partial void OnIdentificationChanged(string value)=>NewOwner=null;
    [RelayCommand] private Task FindAsync()=>WorkAsync(async()=>{Require();Required(Identification,"La identificación",50);NewOwner=null;var result=await api.FindUserAsync(Identification);if(Accepted(result))NewOwner=result.Value;});
    [RelayCommand] private Task SaveAsync()=>WorkAsync(async()=>
    {
        if(Completed||WriteUncertain||Vehicle is null)return;Require();Required(Identification,"La identificación",50);Required(Reason,"El motivo",500);
        var fresh=await api.FindUserAsync(Identification);if(!Accepted(fresh))return;NewOwner=fresh.Value;
        if(NewOwner!.Status!="ACTIVE"||NewOwner.Id==Vehicle.CurrentOwnerId)throw new UserInputException("El nuevo propietario debe estar activo y ser diferente del actual.");
        if(Vehicle.Type=="CAR"&&NewOwner.MemberType=="STUDENT")throw new UserInputException("Un estudiante no puede recibir un automóvil.");
        if(!await navigation.ConfirmAsync("Transferir vehículo","La transferencia cerrará la propiedad actual y cancelará el registro vigente del propietario anterior. El nuevo propietario deberá renovar el registro antes de ingresar.\n\n¿Transferir a "+NewOwner.FullName+"?"))return;
        Require();if(Mutation(await api.TransferAsync(Vehicle.Id,NewOwner.IdentificationNumber,Reason))){Completed=true;await navigation.GoAsync("admin-vehicle-detail",new Dictionary<string,object>{["vehicleId"]=Vehicle.Id});}
    });
}
public partial class AdminCorrectViewModel(AdminApiService api,IAuthSession session,IUserNavigation navigation):AdminFeatureViewModel(session)
{
    [ObservableProperty] private VehicleResponse? vehicle;
    [ObservableProperty] private string identifier="";
    [ObservableProperty] private string reason="";
    public string Summary=>Vehicle is null?"":$"{VehiclePresentation.TypeName(Vehicle.Type)}\nIdentificador actual: {AdminPresentation.Identifier(Vehicle)}";
    partial void OnVehicleChanged(VehicleResponse? value)=>OnPropertyChanged(nameof(Summary));
    [RelayCommand] private Task SaveAsync()=>WorkAsync(async()=>{if(Completed||WriteUncertain||Vehicle is null)return;Require();Required(Identifier,"El nuevo identificador",Vehicle.Type is "BICYCLE" or "SCOOTER"?100:30);Required(Reason,"El motivo",500);if(!await navigation.ConfirmAsync("Corregir identificador",$"¿Cambiar {AdminPresentation.Identifier(Vehicle)} por {Identifier.Trim()}?"))return;if(Mutation(await api.CorrectAsync(Vehicle.Id,Identifier,Reason))){Completed=true;await navigation.GoAsync("admin-vehicle-detail",new Dictionary<string,object>{["vehicleId"]=Vehicle.Id});}});
}
public partial class AdminPeriodFormViewModel(AdminApiService api,IAuthSession session,IUserNavigation navigation):AdminFeatureViewModel(session)
{
    [ObservableProperty] private AcademicPeriodResponse? period;
    [ObservableProperty] private string name="";
    [ObservableProperty] private DateTime startsOn=MobileDates.Today;
    [ObservableProperty] private DateTime endsOn=MobileDates.Today.AddMonths(6);
    public bool IsNew=>Period is null;
    public bool CanActivate=>Period?.Status=="PLANNED"&&!WriteUncertain;
    public bool CanClose=>Period?.Status=="ACTIVE"&&!WriteUncertain;
    public string State=>Period?.Status??"PLANNED";
    partial void OnPeriodChanged(AcademicPeriodResponse? value){if(value is not null){Name=value.Name;StartsOn=value.StartsOn.ToDateTime(TimeOnly.MinValue);EndsOn=value.EndsOn.ToDateTime(TimeOnly.MinValue);}foreach(var p in new[]{nameof(IsNew),nameof(CanActivate),nameof(CanClose),nameof(State)})OnPropertyChanged(p);}
    [RelayCommand] private Task SaveAsync()=>WorkAsync(async()=>{if(!IsNew||Completed||WriteUncertain)return;Require();Required(Name,"El nombre",50);if(EndsOn.Date<=StartsOn.Date)throw new UserInputException("La fecha final debe ser posterior a la inicial.");var result=await api.CreatePeriodAsync(new(Name.Trim(),DateOnly.FromDateTime(StartsOn),DateOnly.FromDateTime(EndsOn)));if(Mutation(result)){Completed=true;await navigation.GoAsync("admin-periods");}});
    [RelayCommand] private Task ActivateAsync()=>ActionAsync(true);
    [RelayCommand] private Task CloseAsync()=>ActionAsync(false);
    private Task ActionAsync(bool activate)=>WorkAsync(async()=>{Require();if(Period is null||Completed||WriteUncertain||activate&&!CanActivate||!activate&&!CanClose)return;if(!await navigation.ConfirmAsync(activate?"Activar periodo":"Cerrar periodo",activate?"¿Activar este periodo académico?":"¿Cerrar este periodo? Un periodo cerrado no puede reactivarse."))return;if(Mutation(await api.PeriodActionAsync(Period.Id,activate))){Completed=true;await navigation.GoAsync("admin-periods");}});
}
public partial class AdminLotFormViewModel(AdminApiService api,IAuthSession session,IUserNavigation navigation):AdminFeatureViewModel(session)
{
    [ObservableProperty] private ParkingLotResponse? lot;
    [ObservableProperty] private string name="";
    [ObservableProperty] private string campus="";
    [ObservableProperty] private TimeSpan opening=TimeSpan.FromHours(6);
    [ObservableProperty] private TimeSpan closing=TimeSpan.FromHours(22);
    public bool IsExisting=>Lot is not null;
    public string State=>Lot is null?"Nuevo parqueadero: tres zonas estándar creadas por el servidor.":AdminPresentation.Status(Lot.Status);
    public string StatusAction=>Lot?.Status=="ACTIVE"?"DESACTIVAR PARQUEADERO":"ACTIVAR PARQUEADERO";
    partial void OnLotChanged(ParkingLotResponse? value){if(value is not null){Name=value.Name;Campus=value.Campus;Opening=value.OpeningTime.ToTimeSpan();Closing=value.ClosingTime.ToTimeSpan();}OnPropertyChanged(nameof(IsExisting));OnPropertyChanged(nameof(State));OnPropertyChanged(nameof(StatusAction));}
    [RelayCommand] private Task SaveAsync()=>WorkAsync(async()=>
    {
        if(Completed||WriteUncertain)return;Require();Required(Name,"El nombre",150);Required(Campus,"La sede",150);if(Opening>=Closing)throw new UserInputException("La hora de cierre debe ser posterior a la apertura.");
        if(Lot is null){var result=await api.CreateLotAsync(new(Name.Trim(),Campus.Trim(),TimeOnly.FromTimeSpan(Opening),TimeOnly.FromTimeSpan(Closing)));if(!Mutation(result))return;}
        else if(!Mutation(await api.EditLotAsync(Lot.Id,new(Name.Trim(),Campus.Trim(),TimeOnly.FromTimeSpan(Opening),TimeOnly.FromTimeSpan(Closing)))))return;
        Completed=true;await navigation.GoAsync("admin-lots");
    });
    [RelayCommand] private Task StatusAsync()=>WorkAsync(async()=>{if(Lot is null||Completed||WriteUncertain)return;Require();var active=Lot.Status!="ACTIVE";if(!active&&!await navigation.ConfirmAsync("Desactivar parqueadero","¿Desactivar este parqueadero?"))return;if(Mutation(await api.LotStatusAsync(Lot.Id,active))){Completed=true;await navigation.GoAsync("admin-lots");}});
}
public partial class AdminNewsFormViewModel(AdminApiService api,IAuthSession session,IUserNavigation navigation):AdminFeatureViewModel(session)
{
    [ObservableProperty] private NewsResponse? news;
    [ObservableProperty] private string title="";
    [ObservableProperty] private string content="";
    public string State=>News?.Status??"DRAFT";
    public bool CanPublish=>News is not null&&News.Status=="DRAFT"&&Title==News.Title&&Content==News.Content;
    public bool CanArchive=>News is not null&&News.Status!="ARCHIVED";
    public bool CanEditNews=>News?.Status!="ARCHIVED";
    partial void OnTitleChanged(string value)=>OnPropertyChanged(nameof(CanPublish));
    partial void OnContentChanged(string value)=>OnPropertyChanged(nameof(CanPublish));
    partial void OnNewsChanged(NewsResponse? value){if(value is not null){Title=value.Title;Content=value.Content;}foreach(var p in new[]{nameof(State),nameof(CanPublish),nameof(CanArchive),nameof(CanEditNews)})OnPropertyChanged(p);}
    [RelayCommand] private Task SaveAsync()=>WorkAsync(async()=>
    {if(Completed||WriteUncertain||!CanEditNews)return;Require();Required(Title,"El título",200);Required(Content,"El contenido",int.MaxValue);if(News is null){if(!Mutation(await api.CreateNewsAsync(Title,Content)))return;}else if(!Mutation(await api.EditNewsAsync(News.Id,Title,Content)))return;Completed=true;await navigation.GoAsync("admin-news");});
    [RelayCommand] private Task PublishAsync()=>ActionAsync(true);
    [RelayCommand] private Task ArchiveAsync()=>ActionAsync(false);
    private Task ActionAsync(bool publish)=>WorkAsync(async()=>{if(Completed||WriteUncertain||News is null||publish&&!CanPublish||!publish&&!CanArchive)return;Require();if(!await navigation.ConfirmAsync(publish?"Publicar noticia":"Archivar noticia",publish?"¿Publicar esta noticia?":"¿Archivar esta noticia?"))return;if(Mutation(await api.NewsActionAsync(News.Id,publish))){Completed=true;await navigation.GoAsync("admin-news");}});
}
public partial class AdminIncidentDetailViewModel(AdminApiService api,IAuthSession session,IUserNavigation navigation,IFileViewer viewer):AdminFeatureViewModel(session)
{
    public Guid IncidentId {get;set;}
    [ObservableProperty] private IncidentDetailResponse? detail;
    [ObservableProperty] private string resolution="";
    [ObservableProperty] private string cancelReason="";
    public bool IsOpen=>Detail?.Incident.Status=="OPEN";
    public string Summary=>Detail is null?"":$"{GuardPresentation.IncidentType(Detail.Incident.Type)} · {GuardPresentation.IncidentStatus(Detail.Incident.Status)}\n{Detail.Incident.Description}\nOcurrido: {MobileDates.Display(Detail.Incident.OccurredAt)}\nReportado por: {Detail.Incident.ReportedBy}\nParqueadero: {Detail.Incident.ParkingLotId}\nUsuario: {Detail.Incident.UserId}\nVehículo: {Detail.Incident.VehicleId}\nMovimiento: {Detail.Incident.ParkingMovementId}\nResolución: {Detail.Incident.Resolution??"Pendiente"}";
    partial void OnDetailChanged(IncidentDetailResponse? value){OnPropertyChanged(nameof(IsOpen));OnPropertyChanged(nameof(Summary));}
    [RelayCommand] private Task LoadAsync()=>WorkAsync(ReadAsync);
    private async Task ReadAsync(){Require();Detail=null;var result=await api.IncidentAsync(IncidentId);if(Accepted(result)){Detail=result.Value;WriteUncertain=false;}}
    [RelayCommand] private Task ResolveFormAsync()=>WorkAsync(async()=>{Require();if(IsOpen)await navigation.GoAsync("admin-incident-resolve",new Dictionary<string,object>{["incidentId"]=IncidentId});});
    [RelayCommand] private Task ResolveAsync()=>WorkAsync(async()=>{if(!IsOpen||WriteUncertain)return;Require();Required(Resolution,"La resolución",int.MaxValue);if(!await navigation.ConfirmAsync("Resolver incidente","¿Resolver este incidente con la descripción indicada?"))return;if(Mutation(await api.ResolveAsync(IncidentId,Resolution))){await navigation.MessageAsync("Incidente","Incidente resuelto correctamente.");await ReadAsync();}});
    [RelayCommand] private Task CancelAsync()=>WorkAsync(async()=>{if(!IsOpen||WriteUncertain)return;Require();if(!await navigation.ConfirmAsync("Cancelar incidente","¿Cancelar este incidente?"))return;if(Mutation(await api.CancelAsync(IncidentId,CancelReason)))await ReadAsync();});
    [RelayCommand] private Task OpenAsync(IncidentAttachmentResponse? file)=>WorkAsync(async()=>{if(file is null)return;Require();var token=await Session.GetTokenAsync();var result=await api.FileAsync(file.ContentUrl);if(Accepted(result)&&Session.User is not null&&token==await Session.GetTokenAsync())await viewer.OpenAsync(file.OriginalFileName,file.ContentType,result.Value!);});
}
