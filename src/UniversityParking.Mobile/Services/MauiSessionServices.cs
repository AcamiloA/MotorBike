using Microsoft.Extensions.DependencyInjection;
using UniversityParking.Mobile.Core;
using UniversityParking.Mobile.Pages;
namespace UniversityParking.Mobile.Services;
public sealed class SecureSessionStorage : ISecretStorage
{
    public Task<string?> GetAsync(string key) => SecureStorage.Default.GetAsync(key);
    public Task SetAsync(string key, string value) => SecureStorage.Default.SetAsync(key, value);
    public void Remove(string key) => SecureStorage.Default.Remove(key);
}
public sealed class AppNavigation(IServiceProvider services, IAuthSession session) : IAppNavigation, IPublicAuthNavigation
{
    private Window? window;
    public void Attach(Window value) => window = value;
    public Task ShowPasswordRecoveryAsync(Guid? challengeId=null,string? token=null)=>MainThread.InvokeOnMainThreadAsync(async()=>
    {
        if(window is null||await session.GetTokenAsync() is not null)return;
        var vm=services.GetRequiredService<UniversityParking.Mobile.ViewModels.PasswordRecoveryViewModel>();vm.ChallengeId=challengeId;vm.ChallengeToken=token;
        window.Page=new PasswordRecoveryPage(vm);
    });
    public Task ShowLoginAsync(string? message = null) => MainThread.InvokeOnMainThreadAsync(async () =>
    {
        if (window is null) return;
        if (message is not null && await session.GetTokenAsync() is not null) return;
        services.GetService<IFileViewer>()?.ClearCache();
        if (window.Page is LoginPage existing) { existing.ViewModel.ErrorMessage = message ?? ""; return; }
        var page = services.GetRequiredService<LoginPage>(); page.ViewModel.ErrorMessage = message ?? ""; window.Page = page;
    });
    public Task ShowStudentRegistrationAsync() => MainThread.InvokeOnMainThreadAsync(async () =>
    {
        if(window is null || window.Page is not LoginPage)return;
        var previous=window.Page;
        if(session.User is not null || await session.GetTokenAsync() is not null || window.Page!=previous)return;
        window.Page=services.GetRequiredService<RegisterStudentPage>();
    });
    public Task ReturnToLoginAsync(string? message = null) => MainThread.InvokeOnMainThreadAsync(async () =>
    {
        if(window is null || session.User is not null || await session.GetTokenAsync() is not null)return;
        var previous=window.Page;
        if(message is not null && previous is { } page)await page.DisplayAlertAsync("Registro",message,"ACEPTAR");
        if(window.Page!=previous || session.User is not null || await session.GetTokenAsync() is not null)return;
        await ShowLoginAsync();
    });
    public Task ShowAuthenticatedAsync() => MainThread.InvokeOnMainThreadAsync(async () =>
    { if (window is not null && session.User is not null && await session.GetTokenAsync() is not null) window.Page = services.GetRequiredService<AppShell>(); });
}
