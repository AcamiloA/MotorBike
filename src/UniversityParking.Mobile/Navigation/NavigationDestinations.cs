using UniversityParking.Mobile.Pages.User;
using UniversityParking.Mobile.Pages.Guard;
using UniversityParking.Mobile.Pages.Admin;

namespace UniversityParking.Mobile.Navigation;

public static class NavigationDestinations
{
    public static Type Page(string key) => key switch
    {
        "user-home" => typeof(UserHomePage), "my-vehicles" => typeof(MyVehiclesPage),
        "my-history" => typeof(MyHistoryPage), "user-news" => typeof(NewsPage), "user-profile" => typeof(ProfilePage),
        "guard-home" => typeof(GuardHomePage),"guard-access-control"=>typeof(GuardAccessControlPage), "guard-inside" => typeof(VehiclesInsidePage),
        "guard-incidents" => typeof(IncidentsPage), "guard-history" => typeof(ParkingHistoryPage),
        "admin-home" => typeof(AdminDashboardPage), "admin-users" => typeof(AdminUsersPage),
        "admin-vehicles" => typeof(AdminVehiclesPage), "admin-periods" => typeof(AdminPeriodsPage),
        "admin-lots" => typeof(AdminLotsPage), "admin-incidents" => typeof(AdminIncidentsPage),
        "admin-news" => typeof(AdminNewsPage), "admin-history" => typeof(AdminHistoryPage),
        "admin-reports" => typeof(AdminReportsPage), "admin-audit" => typeof(AdminAuditPage),
        _ => throw new ArgumentException("Destino de navegación desconocido.", nameof(key))
    };
}
