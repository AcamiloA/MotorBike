using System.Reflection;
using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;
using UniversityParking.Mobile.Core;
namespace UniversityParking.Mobile.Controls;
public static class FilterPanel
{
    private static readonly HashSet<string> Names=["Search","SelectedType","SelectedStatus","SelectedState","SelectedRole","SelectedRegistration","OwnerIdentification","Identification","Identifier","Plate","Frame","DateFrom","DateTo","DateFilterApplied","SelectedVehicle","SelectedLot","SelectedReport","ActorIdentification","Action","EntityType","EntityId","TargetUserId","TargetVehicleId"];
    public static View Create(View filters,object vm)
    {
        if(filters is ScrollView wrapper && wrapper.Content is View inner){wrapper.Content=null;filters=inner;}
        bool HasDate(View view)=>view is DatePicker || view is Layout l && l.Children.OfType<View>().Any(HasDate) || view is ScrollView s && s.Content is View content && HasDate(content);
        var hasDates=HasDate(filters);
        var fields=vm.GetType().GetProperties().Where(p=>p.CanRead&&p.CanWrite&&Names.Contains(p.Name) && (hasDates||p.Name is not ("DateFrom" or "DateTo" or "DateFilterApplied"))).ToArray();
        Dictionary<string,object?> Read()=>fields.ToDictionary(p=>p.Name,p=>p.GetValue(vm));
        void Write(IReadOnlyDictionary<string,object?> values){foreach(var p in fields.OrderBy(p=>p.Name is "TargetUserId" or "TargetVehicleId"?1:0))p.SetValue(vm,values[p.Name]);}
        var initial=Read();var defaults=new Dictionary<string,object?>(initial);if(hasDates)defaults["DateFilterApplied"]=false;
        var state=new FilterState(defaults);foreach(var item in initial)state.DraftFilters[item.Key]=item.Value;state.Apply();
        var button=new ImageButton{Source="filter_funnel.png"};
        button.SetDynamicResource(VisualElement.StyleProperty,"IconImageButton");
        button.SetBinding(VisualElement.IsEnabledProperty,new Binding("IsNotBusy",source:vm));
        SemanticProperties.SetDescription(button,"Abrir filtros");
        var badge=new Label{FontSize=12,BackgroundColor=Color.FromArgb("#2563EB"),TextColor=Colors.White,Padding=4,HorizontalOptions=LayoutOptions.End,VerticalOptions=LayoutOptions.Start,InputTransparent=true,IsVisible=false};
        badge.Text=state.ActiveCount.ToString();badge.IsVisible=state.CanClear;
        var opened=false;var panelOpen=false;
        if(vm is System.ComponentModel.INotifyPropertyChanged notifying)notifying.PropertyChanged+=(_,e)=>
        {
            if(e.PropertyName is null||!Names.Contains(e.PropertyName))return;
            if(panelOpen){foreach(var item in Read())state.DraftFilters[item.Key]=item.Value;return;}
            if(opened)return;
            foreach(var item in Read())state.DraftFilters[item.Key]=item.Value;state.Apply();badge.Text=state.ActiveCount.ToString();badge.IsVisible=state.CanClear;
        };
        var actions=new VerticalStackLayout();
        void Strip(View view)
        {
            if(view is Layout layout) foreach(var child in layout.Children.OfType<View>().ToArray())
            {
                if(child is Button b && (b.Text.Contains("FILTR",StringComparison.OrdinalIgnoreCase)||b.Text=="CONSULTAR REPORTE"))layout.Children.Remove(child);
                else if(child is Button action && (action.Text.Contains("CREAR")||action.Text=="REGISTRAR INCIDENTE")){layout.Children.Remove(child);actions.Children.Add(action);}
                else Strip(child);
            }
            else if(view is ScrollView scroll && scroll.Content is View content) Strip(content);
        }
        Strip(filters);if(filters is ScrollView original)original.HeightRequest=-1;
        var icon=new Grid{WidthRequest=52,HorizontalOptions=LayoutOptions.End};icon.Add(button);icon.Add(badge);
        var root=new FilterPanelHost{Spacing=6,Funnel=icon};root.Add(icon);root.Add(actions);
        async Task Refresh(){var command=vm.GetType().GetProperty("RefreshCommand")?.GetValue(vm);if(command is IAsyncRelayCommand asyncCommand)await asyncCommand.ExecuteAsync(null);else if(command is ICommand sync)sync.Execute(null);}
        button.Clicked+=async(_,_)=>
        {
            if(Application.Current?.Windows.FirstOrDefault()?.Page is not Page owner)return;
            if(panelOpen)return;opened=true;panelOpen=true;
            var lotOwner=vm.GetType().GetProperty("Lots")?.GetValue(vm);
            var lotProperty=lotOwner?.GetType().GetProperty("Selected");var previousLot=lotProperty?.GetValue(lotOwner);
            state.Open();Write(state.DraftFilters);
            var page=new ContentPage{Title="Filtros",BindingContext=vm};var accepted=false;
            var apply=new Button{Text="APLICAR FILTROS"};var clear=new Button{Text="BORRAR FILTROS",IsEnabled=state.CanClear};
            apply.SetDynamicResource(VisualElement.StyleProperty,"PrimaryButton");
            clear.SetDynamicResource(VisualElement.StyleProperty,"SecondaryButton");
            apply.SetBinding(VisualElement.IsEnabledProperty,"IsNotBusy");
            clear.HorizontalOptions=LayoutOptions.Start;
            var panel=new VerticalStackLayout{Spacing=12,Padding=20};panel.Add(filters);panel.Add(new VerticalStackLayout{Spacing=8,Children={apply,clear}});
            page.Content=new ScrollView{Content=panel};
            apply.Clicked+=async(_,_)=>{if(accepted)return;accepted=true;state.DraftFilters.Clear();foreach(var x in Read())state.DraftFilters[x.Key]=x.Value;state.Apply();Write(state.AppliedFilters);badge.Text=state.ActiveCount.ToString();badge.IsVisible=state.CanClear;await owner.Navigation.PopModalAsync();await Refresh();};
            clear.Clicked+=async(_,_)=>{if(accepted)return;accepted=true;state.Clear();Write(state.AppliedFilters);badge.IsVisible=false;await owner.Navigation.PopModalAsync();await Refresh();};
            page.Disappearing+=(_,_)=>{if(!accepted){Write(state.AppliedFilters);if(lotProperty is not null)lotProperty.SetValue(lotOwner,previousLot);}panelOpen=false;panel.Children.Remove(filters);};
            await owner.Navigation.PushModalAsync(page);
        };
        return root;
    }
}
