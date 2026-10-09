using Microsoft.Extensions.DependencyInjection;
using UniversityParking.Mobile.Core;
using UniversityParking.Mobile.Navigation;
using UniversityParking.Mobile.ViewModels;
using UniversityParking.Mobile.Views;

namespace UniversityParking.Mobile;

public partial class AppShell : Shell
{
    private readonly FlyoutMenuViewModel menu;
    public AppShell(IAuthSession session, IServiceProvider services, AuthService auth)
    {
        InitializeComponent();
        var user = session.User;
        var roles = user?.Roles ?? [];
        // Each role has only one root content: no top tabs or duplicated primary pages.
        // Existing global routes supply modules and details through their DI factories.
        var root = new FlyoutItem { Route = "app", Title = "MOTOBIKE PARK" };
        foreach (var role in RoleNavigation.Areas(roles))
        {
            var area = new Tab { Route = role.ToLowerInvariant(), Title = RoleNavigation.Title(role) };
            foreach (var item in NavigationMenu.Catalog.Where(x => x.Role == role && x.Leaf == "home"))
            {
                var pageType = NavigationDestinations.Page(item.Key);
                area.Items.Add(new ShellContent { Route = item.Leaf, Title = item.Title,
                    ContentTemplate = new DataTemplate(() => services.GetRequiredService(pageType)) });
            }
            root.Items.Add(area);
        }
        menu = new FlyoutMenuViewModel(user?.FullName ?? "Mi cuenta", roles, async item =>
        {
            if (Shell.Current != this || session.User?.Id != user?.Id || session.User?.Roles.Contains(item.Role) != true) return;
            await GoToAsync(item.Route);
            if (Shell.Current == this && session.User?.Id == user?.Id) FlyoutIsPresented = false;
        }, async () =>
        {
            if (Shell.Current != this || session.User?.Id != user?.Id) return;
            FlyoutIsPresented = false;
            await auth.LogoutAsync();
        },user?.UserType);
        FlyoutContent = new FlyoutMenuView(menu);
        Items.Add(root);
        var first = NavigationMenu.Catalog.FirstOrDefault(x => roles.Contains(x.Role));
        if (first is not null) menu.Select(first.Key);
    }
    protected override void OnPropertyChanged(string? propertyName = null)
    {
        base.OnPropertyChanged(propertyName);
        if (propertyName == nameof(FlyoutIsPresented) && FlyoutIsPresented && menu?.ActiveKey is { } key)
            menu.Select(key);
    }
    protected override void OnNavigated(ShellNavigatedEventArgs args)
    {
        base.OnNavigated(args);
        if (menu is null) return;
        var key = NavigationMenu.ActiveKey(args.Current.Location.OriginalString);
        if (key is not null) menu.Select(key);
    }
}
