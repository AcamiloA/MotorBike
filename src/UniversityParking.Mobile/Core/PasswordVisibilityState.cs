namespace UniversityParking.Mobile.Core;
public sealed class PasswordVisibilityState
{
    public bool Visible { get; private set; }
    public string Icon => Visible ? "password_eye_off.png" : "password_eye.png";
    public string Description => Visible ? "Ocultar contraseña" : "Mostrar contraseña";
    public (int Cursor,int Selection) Toggle(int cursor,int selection,int length)
    {
        Visible=!Visible;
        var position=Math.Clamp(cursor,0,Math.Max(0,length));
        return(position,Math.Clamp(selection,0,Math.Max(0,length-position)));
    }
}
