using CommunityToolkit.Mvvm.ComponentModel;

namespace UniversityParking.Mobile.Core;

public sealed record NavigationItem(string Key, string Title, string Icon, string Role, string Leaf)
{
    public string RootRoute => $"//app/{Role.ToLowerInvariant()}/home";
    public string Route => Leaf == "home" ? RootRoute : $"{RootRoute}/{Key}";
    public string LegacyRoute => $"//app/{Role.ToLowerInvariant()}/{Leaf}";
}

public sealed class NavigationSection(string key, string title, string role, IReadOnlyList<NavigationItem> items) : ObservableObject
{
    public string Key { get; } = key;
    public string Title { get; } = title;
    public string Role { get; } = role;
    public string Icon => Role switch { "GUARD" => "menu_access.png", "ADMIN" => "menu_report.png", _ => "menu_profile.png" };
    public IReadOnlyList<NavigationItem> Items { get; } = items;
    private bool expanded;
    public bool IsExpanded { get => expanded; set { if (SetProperty(ref expanded, value)) OnPropertyChanged(nameof(Heading)); } }
    public string Heading => $"{(IsExpanded ? "−" : "+")} {Title}";
}

public static class NavigationMenu
{
    public static IReadOnlyList<NavigationItem> Catalog { get; } = new NavigationItem[]
    {
        new("user-home", "Inicio", "menu_home.png", "USER", "home"),
        new("my-vehicles", "Mis vehículos", "menu_vehicle.png", "USER", "vehicles"),
        new("my-history", "Mi historial", "menu_history.png", "USER", "history"),
        new("user-news", "Noticias", "menu_news.png", "USER", "news"),
        new("user-profile", "Perfil", "menu_profile.png", "USER", "profile"),
        new("guard-home", "Registro de acceso", "menu_access.png", "GUARD", "home"),
        new("guard-inside", "Vehículos dentro", "menu_vehicle.png", "GUARD", "inside"),
        new("guard-incidents", "Incidentes de portería", "menu_incident.png", "GUARD", "incidents"),
        new("guard-history", "Historial de parqueo", "menu_history.png", "GUARD", "history"),
        new("admin-home", "Dashboard", "menu_home.png", "ADMIN", "home"),
        new("admin-users", "Usuarios", "menu_profile.png", "ADMIN", "users"),
        new("admin-vehicles", "Vehículos", "menu_vehicle.png", "ADMIN", "vehicles"),
        new("admin-periods", "Periodos académicos", "menu_history.png", "ADMIN", "periods"),
        new("admin-lots", "Parqueaderos", "menu_access.png", "ADMIN", "lots"),
        new("admin-incidents", "Incidentes", "menu_incident.png", "ADMIN", "incidents"),
        new("admin-news", "Noticias", "menu_news.png", "ADMIN", "news"),
        new("admin-history", "Historial general", "menu_history.png", "ADMIN", "history"),
        new("admin-reports", "Reportes", "menu_report.png", "ADMIN", "reports"),
        new("admin-audit", "Auditoría", "menu_report.png", "ADMIN", "audit")
    };

    public static string Resolve(string route, IReadOnlyCollection<string>? roles = null)
    {
        if (route == "admin-account" && roles?.Contains("USER") == true) route = "user-profile";
        return Catalog.FirstOrDefault(x => x.Key == route || x.LegacyRoute == route)?.Route ?? route;
    }

    public static string? ActiveKey(string location)
    {
        var segments = location.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length < 3 || segments[0] != "app") return null;
        var role = segments[1].ToUpperInvariant();
        // Secondary detail/form routes retain the selected module in their parent stack.
        return Catalog.LastOrDefault(x => x.Role == role && segments.Skip(3).Contains(x.Key))?.Key
            ?? Catalog.FirstOrDefault(x => x.Role == role && x.Leaf == segments[2])?.Key;
    }

    public static IReadOnlyList<NavigationSection> Sections(IReadOnlyCollection<string> roles) =>
        RoleNavigation.Areas(roles).Select(role => new NavigationSection(role, RoleNavigation.Title(role), role,
            Catalog.Where(x => x.Role == role && x.Key != "user-home").ToArray())).ToArray();
}

