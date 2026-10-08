using UniversityParking.Mobile.ViewModels;
namespace UniversityParking.Mobile.Pages;
public partial class StartupPage : ContentPage
{
    private readonly StartupViewModel viewModel;
    private bool started;
    public StartupPage(StartupViewModel value) { InitializeComponent(); BindingContext = viewModel = value; }
    protected override async void OnAppearing()
    { base.OnAppearing(); if (started) return; started = true; await viewModel.RestoreCommand.ExecuteAsync(null); }
}