using UniversityParking.Mobile.ViewModels;
namespace UniversityParking.Mobile.Pages;
public partial class SessionHomePage : ContentPage
{
    public SessionHomeViewModel ViewModel { get; }
    public SessionHomePage(SessionHomeViewModel viewModel) { InitializeComponent(); BindingContext = ViewModel = viewModel; }
}