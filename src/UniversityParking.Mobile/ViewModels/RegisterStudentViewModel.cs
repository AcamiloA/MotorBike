using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using UniversityParking.Contracts.Auth;
using UniversityParking.Contracts.Universities;
using UniversityParking.Mobile.Core;

namespace UniversityParking.Mobile.ViewModels;

public partial class RegisterStudentViewModel(StudentRegistrationApiService api, IPublicAuthNavigation navigation) : UserFeatureViewModel
{
    [ObservableProperty] private string identificationNumber="";
    [ObservableProperty] private string fullName="";
    [ObservableProperty] private string career="";
    [ObservableProperty] private string cardCode="";
    [ObservableProperty] private string password="";
    [ObservableProperty] private string confirmPassword="";
    [ObservableProperty,NotifyPropertyChangedFor(nameof(CanSubmit))] private UniversityResponse? selectedUniversity;
    [ObservableProperty,NotifyPropertyChangedFor(nameof(CanSubmit),nameof(CanSelectUniversity))] private bool catalogLoaded;
    [ObservableProperty,NotifyPropertyChangedFor(nameof(CanSubmit),nameof(CanSelectUniversity))] private bool completed;
    [ObservableProperty,NotifyPropertyChangedFor(nameof(CanSubmit),nameof(RegistrationNotice))] private bool writeUncertain;
    public ObservableCollection<UniversityResponse> Universities {get;}=[];
    public bool CanSubmit=>!IsBusy&&CatalogLoaded&&SelectedUniversity is not null&&Universities.Contains(SelectedUniversity)&&!Completed&&!WriteUncertain;
    public bool CanSelectUniversity=>!IsBusy&&CatalogLoaded&&!Completed;
    public string RegistrationNotice=>WriteUncertain?"No se confirmó el registro. Revisa tu cuenta en Login antes de repetirlo.":"";
    private bool submitting;
    private int lifecycle;
    public void Leave(){lifecycle++;if(submitting&&!Completed)WriteUncertain=true;Password="";ConfirmPassword="";}
    protected override void OnPropertyChanged(System.ComponentModel.PropertyChangedEventArgs e)
    {base.OnPropertyChanged(e);if(e.PropertyName==nameof(IsBusy)){base.OnPropertyChanged(new System.ComponentModel.PropertyChangedEventArgs(nameof(CanSubmit)));base.OnPropertyChanged(new System.ComponentModel.PropertyChangedEventArgs(nameof(CanSelectUniversity)));}}

    [RelayCommand] private Task LoadAsync()=>WorkAsync(async()=>
    {
        var version=lifecycle;var previous=SelectedUniversity?.Id;CatalogLoaded=false;SelectedUniversity=null;Universities.Clear();
        var result=await api.UniversitiesAsync();if(version!=lifecycle||!Accepted(result))return;
        var values=result.Value!;
        if(values.Any(x=>x.Id==Guid.Empty||string.IsNullOrWhiteSpace(x.Name))||values.Select(x=>x.Id).Distinct().Count()!=values.Length)
            throw new UserInputException("No fue posible cargar las universidades. Intenta nuevamente.");
        foreach(var value in values)Universities.Add(value);
        SelectedUniversity=Universities.FirstOrDefault(x=>x.Id==previous);CatalogLoaded=true;
        if(values.Length==0)ErrorMessage="No hay universidades activas disponibles.";
    });

    [RelayCommand] private Task SubmitAsync()=>WorkAsync(async()=>
    {
        if(Completed||WriteUncertain)return;
        if(!CatalogLoaded)throw new UserInputException("Carga las universidades antes de registrar.");
        if(SelectedUniversity is null||!Universities.Contains(SelectedUniversity))throw new UserInputException("Selecciona una universidad.");
        Required(IdentificationNumber,"La identificación",50);Required(FullName,"El nombre",200);Required(Career,"La carrera",200);Required(CardCode,"El código de carné",150);
        if(!PasswordPresentation.IsValid(Password))throw new UserInputException("La contraseña requiere 8 caracteres, mayúscula, minúscula y número.");
        if(Password!=ConfirmPassword)throw new UserInputException("Las contraseñas no coinciden.");
        var version=lifecycle;submitting=true;
        try
        {
            var result=await api.RegisterAsync(new RegisterStudentRequest(IdentificationNumber.Trim(),FullName.Trim(),SelectedUniversity.Id,Career.Trim(),CardCode.Trim(),Password));
            Password="";ConfirmPassword="";
            if(version!=lifecycle)return;
            if(!Accepted(result)){if(GuardPresentation.Uncertain(result.Error))WriteUncertain=true;return;}
            if(result.Value is not {UserId:var id,Status:var status}||id==Guid.Empty||status is not ("ACTIVE" or "PENDING"))
            {WriteUncertain=true;throw new UserInputException("No fue posible confirmar el registro. Consulta el estado antes de repetirlo.");}
            Completed=true;
            await navigation.ReturnToLoginAsync(status=="ACTIVE"?"Tu cuenta fue creada correctamente. Ya puedes iniciar sesión.":"Tu registro fue recibido y está pendiente de aprobación.");
        }
        finally{submitting=false;Password="";ConfirmPassword="";}
    });
    [RelayCommand] private Task BackAsync()=>WorkAsync(async()=>{Leave();await navigation.ReturnToLoginAsync();});
}
