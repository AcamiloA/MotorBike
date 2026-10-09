namespace UniversityParking.Mobile.Controls;

public sealed class PasswordField : ContentView
{
    public static readonly BindableProperty TextProperty=BindableProperty.Create(nameof(Text),typeof(string),typeof(PasswordField),"",BindingMode.TwoWay,propertyChanged:(b,o,n)=>((PasswordField)b).SetText((string?)n));
    public string Text { get=>(string)GetValue(TextProperty);set=>SetValue(TextProperty,value); }
    public string Placeholder {get=>InputEntry.Placeholder;set=>InputEntry.Placeholder=value;}
    public ReturnType ReturnType {get=>InputEntry.ReturnType;set=>InputEntry.ReturnType=value;}
    public Entry InputEntry {get;}
    public event EventHandler? Completed;
    private bool syncing,visible;
    private readonly ImageButton toggle;
    public PasswordField():this(new Entry()) { }
    public PasswordField(Entry entry)
    {
        InputEntry=entry;InputEntry.IsPassword=true;InputEntry.IsTextPredictionEnabled=false;
        InputEntry.Completed+=(_,e)=>Completed?.Invoke(this,e);
        InputEntry.TextChanged+=(_,e)=>{if(!syncing)SetValue(TextProperty,e.NewTextValue??"");};
        toggle=new ImageButton{Source="password_eye.png",WidthRequest=48,HeightRequest=48,Padding=12,BackgroundColor=Color.FromArgb("#334155"),CornerRadius=12};
        SemanticProperties.SetDescription(toggle,"Mostrar contraseña");
        toggle.HandlerChanged+=(_,_)=>
        {
#if ANDROID
            if(toggle.Handler?.PlatformView is global::Android.Views.View native){native.Focusable=false;native.FocusableInTouchMode=false;}
#endif
        };
        toggle.Clicked+=(_,_)=>
        {
            var cursor=InputEntry.CursorPosition;var selection=InputEntry.SelectionLength;
            visible=!visible;InputEntry.IsPassword=!visible;
            toggle.Source=visible?"password_eye_off.png":"password_eye.png";
            InputEntry.CursorPosition=Math.Min(cursor,InputEntry.Text?.Length??0);InputEntry.SelectionLength=selection;
            SemanticProperties.SetDescription(toggle,visible?"Ocultar contraseña":"Mostrar contraseña");
        };
        var grid=new Grid{ColumnDefinitions={new ColumnDefinition(GridLength.Star),new ColumnDefinition(GridLength.Auto)},ColumnSpacing=8};
        grid.Add(InputEntry);grid.Add(toggle,1);Content=grid;
    }
    private void SetText(string? value)
    {
        if(InputEntry.Text==value)return;syncing=true;InputEntry.Text=value??"";syncing=false;
    }
    public new bool Focus()=>InputEntry.Focus();
    public new void Unfocus()=>InputEntry.Unfocus();
}
