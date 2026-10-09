namespace UniversityParking.Mobile.Controls;
public sealed class FilterPanelHost:VerticalStackLayout
{
    public View? Funnel {get;set;}
    public View? DetachFunnel()
    {var value=Funnel;if(value is not null)Children.Remove(value);return value;}
}
