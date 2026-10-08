using Microsoft.Extensions.DependencyInjection;
using UniversityParking.Mobile.Pages;
using UniversityParking.Mobile.Services;
using UniversityParking.Mobile.Core;
namespace UniversityParking.Mobile;
public partial class App : Application
{
    private readonly IServiceProvider services;
    private readonly AppNavigation navigation;
    public App(IServiceProvider services, AppNavigation navigation)
    { InitializeComponent(); this.services = services; this.navigation = navigation; UserAppTheme = AppTheme.Dark; }
    protected override Window CreateWindow(IActivationState? activationState)
    { services.GetRequiredService<IFileViewer>().ClearCache(); var window = new Window(services.GetRequiredService<StartupPage>()); navigation.Attach(window); return window; }
}
