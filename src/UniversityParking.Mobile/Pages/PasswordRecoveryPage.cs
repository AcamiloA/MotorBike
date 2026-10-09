using UniversityParking.Mobile.ViewModels;
using UniversityParking.Mobile.Pages.User;
namespace UniversityParking.Mobile.Pages;
public sealed class PasswordRecoveryPage:UserPage<PasswordRecoveryViewModel>
{
    public PasswordRecoveryPage(PasswordRecoveryViewModel vm):base(vm,"Cambiar contraseña")
    {
        var request=UserViews.Stack(UserViews.Input(UserViews.Entry("Email","Correo")),UserViews.Button("SOLICITAR CÓDIGO","RequestCommand"),UserViews.Bound("Notice",true));
        request.SetBinding(IsVisibleProperty,"IsRecovery");
        var code=UserViews.Input(UserViews.Entry("Code","Código recibido"));code.SetBinding(IsVisibleProperty,"IsRecovery");
        var reset=UserViews.Stack(code,UserViews.Input(UserViews.Entry("NewPassword","Nueva contraseña",true)),UserViews.Input(UserViews.Entry("ConfirmPassword","Confirmar nueva contraseña",true)),UserViews.Button("CAMBIAR CONTRASEÑA","SaveCommand"));reset.SetBinding(IsVisibleProperty,"CanEnterPassword");
        Form(request,reset,UserViews.Button("VOLVER A LOGIN","BackCommand"));
    }
}
