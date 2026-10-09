namespace UniversityParking.Mobile.Controls;
public sealed class UppercaseBehavior:Behavior<Entry>
{
    private bool changing;
    protected override void OnAttachedTo(Entry entry){base.OnAttachedTo(entry);entry.TextChanged+=Changed;}
    protected override void OnDetachingFrom(Entry entry){entry.TextChanged-=Changed;base.OnDetachingFrom(entry);}
    private void Changed(object? sender,TextChangedEventArgs e)
    {
        if(changing||sender is not Entry entry||e.NewTextValue is null)return;
        var upper=e.NewTextValue.ToUpperInvariant();if(upper==e.NewTextValue)return;
        var cursor=entry.CursorPosition;var selected=entry.SelectionLength;changing=true;
#if ANDROID
        if(entry.Handler?.PlatformView is global::Android.Widget.EditText native){cursor=Math.Max(0,native.SelectionStart);selected=Math.Max(0,native.SelectionEnd-cursor);}
#endif
        try{entry.Text=upper;entry.CursorPosition=Math.Min(cursor,upper.Length);entry.SelectionLength=selected;}
        finally{changing=false;}
    }
}
