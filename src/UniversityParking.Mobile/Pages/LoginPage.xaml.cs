using UniversityParking.Mobile.ViewModels;
namespace UniversityParking.Mobile.Pages;
public partial class LoginPage : ContentPage
{
    public LoginViewModel ViewModel { get; }
    public LoginPage(LoginViewModel viewModel) { InitializeComponent(); BindingContext = ViewModel = viewModel; }
}