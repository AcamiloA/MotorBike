using Android.App;
using Android.Content.PM;
using Android.OS;
using Android.Views;
using Android.Views.InputMethods;
using AndroidX.Activity;
using AndroidX.Core.View;
using UniversityParking.Mobile.Core;

namespace UniversityParking.Mobile;

[Activity(Theme = "@style/Maui.SplashTheme", MainLauncher = true, LaunchMode = LaunchMode.SingleTop, ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
public class MainActivity : MauiAppCompatActivity
{
    private readonly KeyboardBackState keyboard = new();
    private KeyboardBackCallback? keyboardBack;
    private KeyboardLayoutListener? keyboardLayout;
    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
        keyboardBack = new KeyboardBackCallback(this);
        OnBackPressedDispatcher.AddCallback(this, keyboardBack);
        keyboardLayout = new KeyboardLayoutListener(this);
        Window?.DecorView.ViewTreeObserver?.AddOnGlobalLayoutListener(keyboardLayout);
    }
    private bool OverlayPresented => Microsoft.Maui.Controls.Shell.Current?.FlyoutIsPresented == true ||
        Microsoft.Maui.Controls.Application.Current?.Windows.FirstOrDefault()?.Page?.Navigation.ModalStack.Count > 0;
    private void UpdateKeyboard()
    {
        var insets = Window?.DecorView is { } root ? ViewCompat.GetRootWindowInsets(root) : null;
        var visible = insets?.IsVisible(WindowInsetsCompat.Type.Ime()) == true;
        // Android may consume Back itself to dismiss IME. Release focus after that transition too.
        if (keyboard.Update(visible)) ReleaseEditorFocus();
        if (keyboardBack is not null) keyboardBack.Enabled = keyboard.ShouldDismiss(OverlayPresented);
    }
    private void DismissKeyboard()
    {
        var focus = CurrentFocus;
        if (GetSystemService(InputMethodService) is InputMethodManager manager)
            manager.HideSoftInputFromWindow(focus?.WindowToken ?? Window?.DecorView.WindowToken, HideSoftInputFlags.None);
        ReleaseEditorFocus();
        keyboard.Update(false);
        if (keyboardBack is not null) keyboardBack.Enabled = false;
    }
    private void ReleaseEditorFocus()
    {
        if (CurrentFocus is not global::Android.Widget.EditText editor || !editor.OnCheckIsTextEditor()) return;
        // Keep Android focus search from immediately returning to the same Entry.
        var root = Window?.DecorView;
        if (root is not null) root.FocusableInTouchMode = true;
        editor.ClearFocus(); root?.RequestFocus();
    }
    protected override void OnDestroy()
    {
        if (keyboardLayout is not null)
        {
            Window?.DecorView.ViewTreeObserver?.RemoveOnGlobalLayoutListener(keyboardLayout);
            keyboardLayout.Dispose(); keyboardLayout = null;
        }
        keyboardBack?.Remove(); keyboardBack?.Dispose(); keyboardBack = null;
        base.OnDestroy();
    }
    private sealed class KeyboardLayoutListener(MainActivity activity) : Java.Lang.Object, ViewTreeObserver.IOnGlobalLayoutListener
    { public void OnGlobalLayout() => activity.UpdateKeyboard(); }
    private sealed class KeyboardBackCallback(MainActivity activity) : OnBackPressedCallback(false)
    {
        public override void HandleOnBackPressed()
        {
            activity.UpdateKeyboard();
            if (activity.keyboard.ShouldDismiss(activity.OverlayPresented)) activity.DismissKeyboard();
            else { Enabled = false; activity.OnBackPressedDispatcher.OnBackPressed(); }
        }
    }
}
