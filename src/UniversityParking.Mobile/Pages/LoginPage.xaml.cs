using UniversityParking.Mobile.ViewModels;
using UniversityParking.Mobile.Core;
namespace UniversityParking.Mobile.Pages;
public partial class LoginPage : ContentPage
{
    private readonly LoginInputCoordinator inputs;
    public LoginViewModel ViewModel { get; }
    public LoginPage(LoginViewModel viewModel, LoginInputCoordinator inputs) { this.inputs = inputs; InitializeComponent(); BindingContext = ViewModel = viewModel; }
    private void IdentificationCompleted(object? sender, EventArgs e) { if (ViewModel.CanStart) PasswordEntry.Focus(); }
    private async void PasswordCompleted(object? sender, EventArgs e) { if (ViewModel.CanStart) await ViewModel.SignInCommand.ExecuteAsync(null); }
    private Task ReleaseInputsAsync() => MainThread.InvokeOnMainThreadAsync(async () =>
    {
        foreach (var entry in new[] { IdentificationEntry, PasswordEntry })
        {
            if (!entry.IsFocused) continue;
            try { if (entry.Handler is not null) await entry.HideSoftInputAsync(CancellationToken.None); }
            catch (Exception) { /* The Android fallback below only acts on this page's focused view. */ }
#if ANDROID
            if (entry.Handler?.PlatformView is Android.Views.View native) UniversityParking.Mobile.Platforms.Android.LoginIme.Release(native);
#endif
            entry.Unfocus();
        }
    });
    protected override void OnAppearing() { base.OnAppearing(); IdentificationEntry.Unfocus(); PasswordEntry.Unfocus(); inputs.Attach(this, ReleaseInputsAsync); }
    protected override void OnDisappearing() { inputs.Detach(this); _ = ReleaseInputsAsync(); base.OnDisappearing(); }
}
