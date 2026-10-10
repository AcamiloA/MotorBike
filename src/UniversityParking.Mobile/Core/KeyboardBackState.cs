namespace UniversityParking.Mobile.Core;

public sealed class KeyboardBackState
{
    public bool KeyboardVisible { get; private set; }
    public bool Update(bool visible)
    {
        var releaseFocus = KeyboardVisible && !visible;
        KeyboardVisible = visible;
        return releaseFocus;
    }
    public bool ShouldDismiss(bool overlayPresented) => KeyboardVisible && !overlayPresented;
}
