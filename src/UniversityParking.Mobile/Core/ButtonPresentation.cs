namespace UniversityParking.Mobile.Core;

public enum ButtonKind { Primary, Secondary, Danger, Icon }
public static class ButtonPresentation
{
    public static ButtonKind ForCommand(string command) => command switch
    {
        "ArchiveCommand" or "DeleteCommand" or "RejectRegistrationCommand" => ButtonKind.Danger,
        "LoadCommand" or "LoadPhotoCommand" or "RestoreCommand" or "UpdateCommand" => ButtonKind.Icon,
        "SaveCommand" or "SubmitCommand" or "SignInCommand" or "ConfirmCommand" or "CreateCommand" or
        "RegisterCommand" or "RenewCommand" or "ApproveRegistrationCommand" or "RequestCommand" or
        "ResolveCommand" or "ResetPasswordCommand" or "SearchCommand" or "SelectCommand" or
        "ContinueCommand" or "CorrectCommand" or "TransferCommand" or "ExitCommand" => ButtonKind.Primary,
        _ => ButtonKind.Secondary
    };
    public static string Style(ButtonKind kind) => kind switch
    {
        ButtonKind.Primary => "PrimaryButton",
        ButtonKind.Danger => "DangerButton",
        ButtonKind.Icon => "IconButton",
        _ => "SecondaryButton"
    };
}
