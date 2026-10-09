using Android.Views;
using Android.Views.InputMethods;

namespace UniversityParking.Mobile.Platforms.Android;

internal static class LoginIme
{
    public static void Release(global::Android.Views.View field)
    {
        var activity = Microsoft.Maui.ApplicationModel.Platform.CurrentActivity;
        if (activity?.CurrentFocus != field) return;
        if (activity.GetSystemService(global::Android.Content.Context.InputMethodService) is InputMethodManager ime)
            ime.HideSoftInputFromWindow(field.WindowToken, HideSoftInputFlags.None);
        field.ClearFocus();
    }
}
