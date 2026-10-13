using UniversityParking.Mobile.ViewModels;
using UniversityParking.Mobile.Core;
using UniversityParking.Mobile.Pages.User;
namespace UniversityParking.Mobile.Pages;
public partial class StartupPage : ContentPage
{
    private readonly StartupViewModel viewModel;
    private readonly AutomaticPageLoadState loading = new();
    public StartupPage(StartupViewModel value)
    { InitializeComponent(); BindingContext = viewModel = value; if(Content is View content)Content=UserViews.Refresh(content,"RestoreCommand"); }
    protected override async void OnAppearing()
    { base.OnAppearing(); if (!loading.ShouldLoad(true,viewModel.IsBusy)) return; await viewModel.RestoreCommand.ExecuteAsync(null);loading.Complete(string.IsNullOrEmpty(viewModel.ErrorMessage)); }
}
