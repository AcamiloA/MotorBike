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
public partial class LoginViewModel(AuthService auth) : BusyViewModel
{
    [ObservableProperty] private string identificationNumber = "";
    [ObservableProperty] private string password = "";
    [RelayCommand]
    private async Task SignInAsync()
    {
        if (IsBusy) return;
        if (string.IsNullOrWhiteSpace(IdentificationNumber) || string.IsNullOrWhiteSpace(Password))
        { ErrorMessage = "Ingresa tu identificación y contraseña."; return; }
        IsBusy = true; ErrorMessage = "";
        try { var result = await auth.LoginAsync(IdentificationNumber, Password); if (!result.IsSuccess) ErrorMessage = result.Error!.Message; }
        catch (Exception) { ErrorMessage = "No fue posible iniciar sesión. Intenta nuevamente."; }
        finally { Password = ""; IsBusy = false; }
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