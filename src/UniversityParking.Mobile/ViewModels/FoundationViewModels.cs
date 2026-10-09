using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using UniversityParking.Mobile.Core;
namespace UniversityParking.Mobile.ViewModels;
public partial class BusyViewModel : ObservableObject
{
    [ObservableProperty, NotifyPropertyChangedFor(nameof(IsNotBusy))] private bool isBusy;
    [ObservableProperty] private string errorMessage = "";
    public bool IsNotBusy => !IsBusy;
}
public partial class LoginViewModel(AuthService auth, IPublicAuthNavigation? publicNavigation = null, ILoginInputLifecycle? inputs = null) : BusyViewModel
{
    private int claimed;
    public bool CanStart => !IsBusy && Volatile.Read(ref claimed) == 0;
    [RelayCommand] private async Task CreateStudentAccountAsync()
    {
        if (IsBusy || Interlocked.CompareExchange(ref claimed, 1, 0) != 0) return;
        OnPropertyChanged(nameof(CanStart)); ErrorMessage="";
        try
        {
            if (inputs is not null) await inputs.PrepareAsync();
            IsBusy=true; Password="";
            if(publicNavigation is null)throw new InvalidOperationException();
            await publicNavigation.ShowStudentRegistrationAsync();
        }
        catch(Exception){ErrorMessage="No fue posible abrir el registro. Intenta nuevamente.";}
        finally { IsBusy=false; Interlocked.Exchange(ref claimed,0); OnPropertyChanged(nameof(CanStart)); }
    }
    [ObservableProperty] private string identificationNumber = "";
    [RelayCommand] private async Task ForgotPasswordAsync()
    {
        if(IsBusy||publicNavigation is null||Interlocked.CompareExchange(ref claimed,1,0)!=0)return;
        OnPropertyChanged(nameof(CanStart));ErrorMessage="";
        try{if(inputs is not null)await inputs.PrepareAsync();IsBusy=true;Password="";await publicNavigation.ShowPasswordRecoveryAsync();}
        catch{ErrorMessage="No fue posible abrir la recuperación. Intenta nuevamente.";}
        finally{IsBusy=false;Interlocked.Exchange(ref claimed,0);OnPropertyChanged(nameof(CanStart));}
    }
    [ObservableProperty] private string password = "";
    [RelayCommand]
    private async Task SignInAsync()
    {
        if (IsBusy || Interlocked.CompareExchange(ref claimed, 1, 0) != 0) return;
        OnPropertyChanged(nameof(CanStart));
        if (string.IsNullOrWhiteSpace(IdentificationNumber) || string.IsNullOrWhiteSpace(Password))
        { ErrorMessage = "Ingresa tu identificación y contraseña."; Interlocked.Exchange(ref claimed,0); OnPropertyChanged(nameof(CanStart)); return; }
        var identification = IdentificationNumber; var password = Password; ErrorMessage = "";
        try
        {
            if (inputs is not null) await inputs.PrepareAsync();
            IsBusy = true;
            var result = await auth.LoginAsync(identification, password); if (!result.IsSuccess) ErrorMessage = result.Error!.Message;
        }
        catch (Exception) { ErrorMessage = "No fue posible iniciar sesión. Intenta nuevamente."; }
        finally { Password = ""; IsBusy = false; Interlocked.Exchange(ref claimed,0); OnPropertyChanged(nameof(CanStart)); }
    }
}
public partial class StartupViewModel(AuthService auth) : BusyViewModel
{
    [RelayCommand]
    private async Task RestoreAsync()
    {
        if (IsBusy) return;
        IsBusy = true; ErrorMessage = "";
        try { var result = await auth.RestoreAsync(); ErrorMessage = result.Message ?? ""; }
        catch (Exception) { ErrorMessage = "No fue posible conectar con el servidor. Intenta nuevamente."; }
        finally { IsBusy = false; }
    }
    [RelayCommand]
    private async Task LogoutAsync()
    {
        try { await auth.LogoutAsync(); }
        catch (Exception) { ErrorMessage = "No fue posible cerrar la sesión segura. Intenta nuevamente."; }
    }
}
public partial class SessionHomeViewModel(IAuthSession session, AuthService auth) : BusyViewModel
{
    public string FullName => session.User?.FullName ?? "";
    public string MemberType => session.User?.MemberType switch { "STUDENT" => "Estudiante", "TEACHER" => "Docente", "STAFF" => "Personal universitario", _ => "" };
    public string Roles => string.Join(" · ", (session.User?.Roles ?? []).Select(RoleNavigation.Title));
    [ObservableProperty] private string areaTitle = "Mi cuenta";
    [RelayCommand]
    private async Task LogoutAsync()
    {
        if (IsBusy) return;
        IsBusy = true; ErrorMessage = "";
        try { await auth.LogoutAsync(); }
        catch (Exception) { ErrorMessage = "No fue posible cerrar la sesión segura. Intenta nuevamente."; }
        finally { IsBusy = false; }
    }
}
