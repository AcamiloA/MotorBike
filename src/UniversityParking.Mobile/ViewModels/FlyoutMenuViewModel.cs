using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using UniversityParking.Mobile.Core;

namespace UniversityParking.Mobile.ViewModels;

public sealed class FlyoutMenuViewModel : ObservableObject
{
    public IReadOnlyList<NavigationSection> Sections { get; }
    public NavigationItem? Home { get; }
    public string UserName { get; }
    public string Roles { get; }
    public IAsyncRelayCommand<NavigationItem> NavigateCommand { get; }
    public IRelayCommand<NavigationSection> ToggleCommand { get; }
    public IAsyncRelayCommand LogoutCommand { get; }
    private string? activeKey;
    public string? ActiveKey { get => activeKey; private set => SetProperty(ref activeKey, value); }
    private string errorMessage = "";
    private bool busy;
    public string ErrorMessage { get => errorMessage; private set => SetProperty(ref errorMessage, value); }

    public FlyoutMenuViewModel(string name, IReadOnlyCollection<string> roles,
        Func<NavigationItem, Task> navigate, Func<Task> logout,string? userType=null)
    {
        UserName = name; Roles = userType switch {"STUDENT"=>"ESTUDIANTE","TEACHER"=>"DOCENTE","ADMINISTRATIVE"=>"ADMINISTRATIVO","GUARD"=>"GUARDA",_=>string.Join(" · ", RoleNavigation.Areas(roles))};
        Sections = NavigationMenu.Sections(roles);
        Home = roles.Contains("USER") ? NavigationMenu.Catalog.Single(x => x.Key == "user-home") : null;
        ToggleCommand = new RelayCommand<NavigationSection>(section =>
        {
            if (section is null || !Sections.Contains(section)) return;
            var expand = !section.IsExpanded;
            foreach (var entry in Sections) entry.IsExpanded = entry == section && expand;
        });
        NavigateCommand = new AsyncRelayCommand<NavigationItem>(async item =>
        {
            if (item is null || item != Home && !Sections.Any(x => x.Items.Contains(item))) return;
            await RunAsync(() => navigate(item), "No fue posible abrir esta opción. Intenta nuevamente.");
        }, _ => !busy);
        LogoutCommand = new AsyncRelayCommand(() => RunAsync(logout,
            "No fue posible cerrar la sesión. Intenta nuevamente."), () => !busy);
    }

    private async Task RunAsync(Func<Task> action, string error)
    {
        if (busy) return;
        busy = true; ErrorMessage = "";
        NavigateCommand.NotifyCanExecuteChanged(); LogoutCommand.NotifyCanExecuteChanged();
        try { await action(); }
        catch (Exception) { ErrorMessage = error; }
        finally
        {
            busy = false;
            NavigateCommand.NotifyCanExecuteChanged(); LogoutCommand.NotifyCanExecuteChanged();
        }
    }

    public void Select(string key)
    {
        var section = Sections.FirstOrDefault(x => x.Items.Any(item => item.Key == key));
        if (key != Home?.Key && section is null) return;
        ActiveKey = key;
        foreach (var entry in Sections) entry.IsExpanded = entry == section;
    }
}
