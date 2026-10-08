using System.Reflection;
using Microsoft.Extensions.Logging;
using UniversityParking.Mobile.Core;
using UniversityParking.Mobile.Pages;
using UniversityParking.Mobile.Services;
using UniversityParking.Mobile.ViewModels;
using UniversityParking.Mobile.Pages.User;
using UniversityParking.Mobile.Pages.Guard;
using UniversityParking.Mobile.Pages.Admin;
using ZXing.Net.Maui.Controls;
namespace UniversityParking.Mobile;
public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder.UseMauiApp<App>().UseBarcodeReader().ConfigureFonts(fonts =>
        {
            fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
            fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
        });
        var url = typeof(MauiProgram).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>().Single(x => x.Key == "ApiBaseUrl").Value ?? "";
#if DEBUG
        var options = new ApiOptions(url, allowHttp: true);
        builder.Logging.AddDebug();
#else
        var options = new ApiOptions(url);
#endif
        builder.Services.AddSingleton(options);
        builder.Services.AddSingleton<ISecretStorage, SecureSessionStorage>();
        builder.Services.AddSingleton<IAuthSession, AuthSession>();
        builder.Services.AddSingleton<AppNavigation>();
        builder.Services.AddSingleton<IAppNavigation>(p => p.GetRequiredService<AppNavigation>());
        builder.Services.AddSingleton(p => new HttpClient(new AuthHttpHandler(p.GetRequiredService<IAuthSession>(),
            p.GetRequiredService<IAppNavigation>(), options) { InnerHandler = new HttpClientHandler { AllowAutoRedirect = false } })
            { BaseAddress = options.BaseAddress, Timeout = TimeSpan.FromSeconds(20) });
        builder.Services.AddSingleton<ApiClient>();
        builder.Services.AddSingleton<AuthService>();
        builder.Services.AddTransient<LoginViewModel>(); builder.Services.AddTransient<LoginPage>();
        builder.Services.AddTransient<StartupViewModel>(); builder.Services.AddTransient<StartupPage>();
        builder.Services.AddTransient<SessionHomeViewModel>(); builder.Services.AddTransient<SessionHomePage>();
        builder.Services.AddTransient<AppShell>();
        builder.Services.AddSingleton<UserApiService>();
        builder.Services.AddSingleton<IUserNavigation, UserNavigation>();
        builder.Services.AddSingleton<IAttachmentPicker, AttachmentPicker>();
        builder.Services.AddSingleton<IFileViewer, FileViewer>();
        builder.Services.AddTransient<UserHomeViewModel>(); builder.Services.AddTransient<UserHomePage>();
        builder.Services.AddTransient<MyVehiclesViewModel>(); builder.Services.AddTransient<MyVehiclesPage>();
        builder.Services.AddTransient<VehicleDetailViewModel>(); builder.Services.AddTransient<VehicleDetailPage>();
        builder.Services.AddTransient<RegisterVehicleViewModel>(); builder.Services.AddTransient<RegisterVehiclePage>();
        builder.Services.AddTransient<EditVehicleViewModel>(); builder.Services.AddTransient<EditVehiclePage>();
        builder.Services.AddTransient<RenewRegistrationViewModel>(); builder.Services.AddTransient<RenewRegistrationPage>();
        builder.Services.AddTransient<MyHistoryViewModel>(); builder.Services.AddTransient<MyHistoryPage>();
        builder.Services.AddTransient<NewsViewModel>(); builder.Services.AddTransient<NewsPage>();
        builder.Services.AddTransient<NewsDetailViewModel>(); builder.Services.AddTransient<NewsDetailPage>();
        builder.Services.AddTransient<ProfileViewModel>(); builder.Services.AddTransient<ProfilePage>();
        builder.Services.AddTransient<EditProfileViewModel>(); builder.Services.AddTransient<EditProfilePage>();
        builder.Services.AddTransient<ChangePasswordViewModel>(); builder.Services.AddTransient<ChangePasswordPage>();
        builder.Services.AddSingleton<GuardApiService>();
        builder.Services.AddSingleton<IGuardSelectionStore, GuardSelectionStore>();
        builder.Services.AddSingleton<IScannerPermission, ScannerPermission>();
        builder.Services.AddSingleton<GuardLotSession>();
        builder.Services.AddTransient<GuardHomeViewModel>(); builder.Services.AddTransient<GuardHomePage>();
        builder.Services.AddTransient<GuardLookupViewModel>(); builder.Services.AddTransient<ScanCardPage>(); builder.Services.AddTransient<ManualSearchPage>();
        builder.Services.AddTransient<AccessResultViewModel>(); builder.Services.AddTransient<AccessResultPage>();
        builder.Services.AddTransient<CheckInViewModel>(); builder.Services.AddTransient<CheckInPage>();
        builder.Services.AddTransient<CheckOutViewModel>(); builder.Services.AddTransient<CheckOutPage>();
        builder.Services.AddTransient<VehiclesInsideViewModel>(); builder.Services.AddTransient<VehiclesInsidePage>();
        builder.Services.AddTransient<ParkingHistoryViewModel>(); builder.Services.AddTransient<ParkingHistoryPage>();
        builder.Services.AddTransient<IncidentsViewModel>(); builder.Services.AddTransient<IncidentsPage>();
        builder.Services.AddTransient<CreateIncidentViewModel>(); builder.Services.AddTransient<CreateIncidentPage>();
        builder.Services.AddTransient<IncidentDetailViewModel>(); builder.Services.AddTransient<IncidentDetailPage>();
        builder.Services.AddSingleton<AdminApiService>();
        foreach(var type in new[] {typeof(AdminDashboardViewModel),typeof(AdminDashboardPage),typeof(AdminUsersViewModel),typeof(AdminUsersPage),typeof(AdminUserFormViewModel),typeof(AdminUserFormPage),typeof(AdminUserDetailViewModel),typeof(AdminUserDetailPage),
            typeof(AdminVehiclesViewModel),typeof(AdminVehiclesPage),typeof(AdminVehicleDetailViewModel),typeof(AdminVehicleDetailPage),typeof(AdminTransferViewModel),typeof(AdminTransferPage),typeof(AdminCorrectViewModel),typeof(AdminCorrectPage),
            typeof(AdminPeriodsViewModel),typeof(AdminPeriodsPage),typeof(AdminPeriodFormViewModel),typeof(AdminPeriodFormPage),typeof(AdminLotsViewModel),typeof(AdminLotsPage),typeof(AdminLotFormViewModel),typeof(AdminLotFormPage),
            typeof(AdminIncidentsViewModel),typeof(AdminIncidentsPage),typeof(AdminIncidentDetailViewModel),typeof(AdminIncidentDetailPage),typeof(AdminResolveIncidentPage),typeof(AdminIncidentCreateViewModel),typeof(AdminIncidentCreatePage),
            typeof(AdminNewsViewModel),typeof(AdminNewsPage),typeof(AdminNewsFormViewModel),typeof(AdminNewsFormPage),typeof(AdminHistoryViewModel),typeof(AdminHistoryPage),typeof(AdminReportsViewModel),typeof(AdminReportsPage),typeof(AdminAuditViewModel),typeof(AdminAuditPage)})builder.Services.AddTransient(type);
        var app = builder.Build(); UserRoutes.Register(app.Services); GuardRoutes.Register(app.Services); AdminRoutes.Register(app.Services); return app;
    }
}
