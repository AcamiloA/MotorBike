using UniversityParking.Mobile.Core;
using UniversityParking.Mobile.ViewModels;

namespace UniversityParking.Mobile.Tests;

public sealed class NavigationMenuTests
{
    [Theory]
    [InlineData("USER", "USER")]
    [InlineData("USER,GUARD", "USER,GUARD")]
    [InlineData("USER,ADMIN", "USER,ADMIN")]
    [InlineData("USER,GUARD,ADMIN", "USER,GUARD,ADMIN")]
    [InlineData("ADMIN", "ADMIN")]
    public void MenuIncludesOnlyExplicitRoles(string roles, string expected)
    {
        var menu = Create(roles.Split(','));
        Assert.Equal(expected.Split(','), menu.Sections.Select(x => x.Role));
        Assert.Equal(roles.Split(',').Contains("USER"), menu.Home is not null);
        var items = menu.Sections.SelectMany(x => x.Items).ToArray();
        Assert.Equal(items.Length, items.Select(x => x.Key).Distinct().Count());
        Assert.Equal(items.Length, items.Select(x => x.Route).Distinct().Count());
        if (!roles.Split(',').Contains("GUARD")) Assert.DoesNotContain(items, x => x.Role == "GUARD");
    }

    [Fact]
    public void AccordionAllowsOneSectionAndCanCollapseIt()
    {
        var menu = Create(["USER", "GUARD", "ADMIN"]);
        menu.ToggleCommand.Execute(menu.Sections[0]);
        Assert.True(menu.Sections[0].IsExpanded);
        menu.ToggleCommand.Execute(menu.Sections[2]);
        Assert.False(menu.Sections[0].IsExpanded);
        Assert.True(menu.Sections[2].IsExpanded);
        menu.ToggleCommand.Execute(menu.Sections[2]);
        Assert.All(menu.Sections, x => Assert.False(x.IsExpanded));
    }

    [Fact]
    public void ActiveDestinationExpandsItsSectionAndDoesNotCrossSessions()
    {
        var admin = Create(["USER", "ADMIN"]);
        admin.Select("admin-users");
        Assert.Equal("admin-users", admin.ActiveKey);
        Assert.True(admin.Sections.Single(x => x.Role == "ADMIN").IsExpanded);
        var nextUser = Create(["USER"]);
        Assert.Null(nextUser.ActiveKey);
        nextUser.Select("admin-users");
        Assert.Null(nextUser.ActiveKey);
        Assert.All(nextUser.Sections, x => Assert.False(x.IsExpanded));
    }

    [Fact]
    public async Task NavigationUsesExistingDestinationAndRejectsHiddenItems()
    {
        string? route = null;
        var menu = new FlyoutMenuViewModel("Usuario", ["USER"], item => { route = item.Route; return Task.CompletedTask; }, () => Task.CompletedTask);
        await menu.NavigateCommand.ExecuteAsync(NavigationMenu.Catalog.Single(x => x.Key == "my-vehicles"));
        Assert.Equal("//app/user/home/my-vehicles", route);
        await menu.NavigateCommand.ExecuteAsync(NavigationMenu.Catalog.Single(x => x.Key == "guard-home"));
        Assert.Equal("//app/user/home/my-vehicles", route);
    }

    [Fact]
    public async Task LogoutDelegatesToExistingFlow()
    {
        var called = 0;
        var menu = new FlyoutMenuViewModel("Usuario", ["USER"], _ => Task.CompletedTask, () => { called++; return Task.CompletedTask; });
        await menu.LogoutCommand.ExecuteAsync(null);
        Assert.Equal(1, called);
    }

    [Fact]
    public void ExistingAliasesAndSecondaryRoutesRemainResolvable()
    {
        Assert.Equal("//app/guard/home", NavigationMenu.Resolve("guard-home"));
        Assert.Equal("//app/user/home/user-profile", NavigationMenu.Resolve("user-profile"));
        Assert.Equal("//app/admin/home/admin-users", NavigationMenu.Resolve("admin-users"));
        Assert.Equal("//app/user/home/my-vehicles", NavigationMenu.Resolve("//app/user/vehicles"));
        Assert.Equal("vehicle-detail", NavigationMenu.Resolve("vehicle-detail"));
        Assert.Equal("..", NavigationMenu.Resolve(".."));
        Assert.Equal(NavigationMenu.Resolve("user-profile"), NavigationMenu.Resolve("admin-account", ["USER", "ADMIN"]));
        Assert.Equal("admin-account", NavigationMenu.Resolve("admin-account", ["ADMIN"]));
    }

    [Theory]
    [InlineData("//app/user/home/my-vehicles/vehicle-detail/vehicle-edit", "my-vehicles")]
    [InlineData("//app/guard/home/guard-incidents/guard-incident-detail", "guard-incidents")]
    [InlineData("//app/admin/home/admin-users/admin-user-detail", "admin-users")]
    [InlineData("//app/guard/home/guard-access-control", "guard-home")]
    public void DetailAndBackStacksRetainTheirOwningMenuItem(string route, string key)
        => Assert.Equal(key, NavigationMenu.ActiveKey(route));

    [Fact]
    public async Task PendingNavigationPreventsAnotherSelectionAndLogout()
    {
        var pending = new TaskCompletionSource<bool>();
        var navigations = 0; var logouts = 0;
        var menu = new FlyoutMenuViewModel("Usuario", ["USER"], _ => { navigations++; return pending.Task; },
            () => { logouts++; return Task.CompletedTask; });
        var item = menu.Home!;
        var first = menu.NavigateCommand.ExecuteAsync(item);
        Assert.False(menu.NavigateCommand.CanExecute(item));
        Assert.False(menu.LogoutCommand.CanExecute(null));
        await menu.NavigateCommand.ExecuteAsync(item);
        await menu.LogoutCommand.ExecuteAsync(null);
        Assert.Equal(1, navigations); Assert.Equal(0, logouts);
        pending.SetResult(true); await first;
        Assert.True(menu.LogoutCommand.CanExecute(null));
    }

    private static FlyoutMenuViewModel Create(string[] roles) => new("Usuario", roles, _ => Task.CompletedTask, () => Task.CompletedTask);
}
