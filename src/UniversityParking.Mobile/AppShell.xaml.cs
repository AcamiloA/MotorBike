using Microsoft.Extensions.DependencyInjection;
using UniversityParking.Mobile.Core;
using UniversityParking.Mobile.Pages;
using UniversityParking.Mobile.Pages.User;
using UniversityParking.Mobile.Pages.Guard;
using UniversityParking.Mobile.Pages.Admin;
namespace UniversityParking.Mobile;
public partial class AppShell : Shell
{
    public AppShell(IAuthSession session, IServiceProvider services)
    {
        InitializeComponent(); var tabs = new TabBar { Route = "app" };
        foreach (var role in RoleNavigation.Areas(session.User?.Roles ?? []))
        {
            var tab = new Tab { Title = RoleNavigation.Title(role), Route = role.ToLowerInvariant() };
            if (role == "USER")
            {
                tab.Title = "Inicio";
                foreach (var item in new[] { ("Inicio", "home", typeof(UserHomePage)), ("Vehículos", "vehicles", typeof(MyVehiclesPage)),
                    ("Historial", "history", typeof(MyHistoryPage)), ("Noticias", "news", typeof(NewsPage)), ("Perfil", "profile", typeof(ProfilePage)) })
                {
                    var type = item.Item3;
                    tab.Items.Add(new ShellContent { Title = item.Item1, Route = item.Item2, ContentTemplate = new DataTemplate(() => services.GetRequiredService(type)) });
                }
                tabs.Items.Add(tab); continue;
            }
            if (role == "GUARD")
            {
                tab.Items.Add(new ShellContent { Title = "Control de acceso", Route = "home", ContentTemplate = new DataTemplate(() => services.GetRequiredService<GuardHomePage>()) });
                tabs.Items.Add(tab); continue;
            }
            tab.Items.Add(new ShellContent { Title = tab.Title, Route = "home", ContentTemplate = new DataTemplate(() => services.GetRequiredService<AdminDashboardPage>()) });
            tabs.Items.Add(tab);
        }
        Items.Add(tabs);
    }
}
