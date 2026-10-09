using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using UniversityParking.Contracts.Auth;
using UniversityParking.Mobile.Core;
namespace UniversityParking.Mobile.ViewModels;
public partial class PasswordRecoveryViewModel(ApiClient api,IPublicAuthNavigation navigation):BusyViewModel
{
    public Guid? ChallengeId {get;set;} public string? ChallengeToken {get;set;}
    public bool IsRecovery=>ChallengeId is null;
    [ObservableProperty,NotifyPropertyChangedFor(nameof(CanEnterPassword))] private bool codeRequested;
    public bool CanEnterPassword=>!IsRecovery||CodeRequested;
    [ObservableProperty] private string email="";
    [ObservableProperty] private string code="";
    [ObservableProperty] private string newPassword="";
    [ObservableProperty] private string confirmPassword="";
    [ObservableProperty] private string notice="";
    [RelayCommand] private async Task RequestAsync()
    {
        if(IsBusy)return;IsBusy=true;ErrorMessage="";
        try{var result=await api.PostAsync<object>("api/v1/auth/password-recovery/request",new RequestPasswordRecoveryRequest(Email.Trim()));if(result.IsSuccess){CodeRequested=true;Notice="Si existe una cuenta asociada a este correo, recibirás instrucciones para restablecer tu contraseña.";}else ErrorMessage=result.Error!.Message;}
        catch{ErrorMessage="No fue posible solicitar la recuperación. Intenta nuevamente.";}finally{IsBusy=false;}
    }
    [RelayCommand] private async Task SaveAsync()
    {
        if(IsBusy)return;
        if(!PasswordPresentation.IsValid(NewPassword)||NewPassword!=ConfirmPassword){ErrorMessage="Verifica la contraseña y su confirmación.";return;}
        IsBusy=true;ErrorMessage="";
        try
        {
            var result=IsRecovery?await api.PostAsync<bool>("api/v1/auth/password-recovery/complete",new CompletePasswordRecoveryRequest(Email.Trim(),Code.Trim(),NewPassword)):
                await api.PostAsync<bool>("api/v1/auth/temporary-password/complete",new CompleteTemporaryPasswordRequest(ChallengeId!.Value,ChallengeToken!,NewPassword));
            if(result.IsSuccess){ChallengeToken=null;await navigation.ReturnToLoginAsync("Contraseña actualizada. Inicia sesión con tu nueva contraseña.");}else ErrorMessage=result.Error!.Message;
        }
        catch{ErrorMessage="No fue posible confirmar el cambio. Intenta iniciar sesión antes de repetirlo.";}
        finally{NewPassword="";ConfirmPassword="";IsBusy=false;}
    }
    [RelayCommand] private Task BackAsync(){ChallengeToken=null;NewPassword=ConfirmPassword=Code="";return navigation.ReturnToLoginAsync();}
}
