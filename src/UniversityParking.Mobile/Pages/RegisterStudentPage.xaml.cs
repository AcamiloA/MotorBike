using UniversityParking.Mobile.ViewModels;

namespace UniversityParking.Mobile.Pages;

public partial class RegisterStudentPage : ContentPage
{
    public RegisterStudentViewModel ViewModel {get;}
    public RegisterStudentPage(RegisterStudentViewModel viewModel){InitializeComponent();BindingContext=ViewModel=viewModel;}
    protected override async void OnAppearing(){base.OnAppearing();await ViewModel.LoadCommand.ExecuteAsync(null);}
    protected override void OnDisappearing(){ViewModel.Leave();base.OnDisappearing();}
    protected override bool OnBackButtonPressed()
    {
        if(ViewModel.IsBusy)return base.OnBackButtonPressed();
        _=ViewModel.BackCommand.ExecuteAsync(null);
        return true;
    }
}
