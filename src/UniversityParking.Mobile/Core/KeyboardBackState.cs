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
    // IME takes priority even inside a modal. Once closed, navigation owns Back.
    public bool ShouldDismiss() => KeyboardVisible;
}
