using UniversityParking.Mobile.Core;
namespace UniversityParking.Mobile.Tests;
public sealed class ButtonPresentationTests
{
    [Theory]
    [InlineData("CancelCommand",ButtonKind.Secondary)] [InlineData("BackCommand",ButtonKind.Secondary)]
    [InlineData("ClearAssociationCommand",ButtonKind.Secondary)] [InlineData("RemoveCommand",ButtonKind.Secondary)]
    [InlineData("ArchiveCommand",ButtonKind.Danger)] [InlineData("RejectRegistrationCommand",ButtonKind.Danger)]
    [InlineData("SaveCommand",ButtonKind.Primary)] [InlineData("ConfirmCommand",ButtonKind.Primary)]
    [InlineData("LoadCommand",ButtonKind.Icon)] [InlineData("LoadPhotoCommand",ButtonKind.Icon)]
    [InlineData("ForgotPasswordCommand",ButtonKind.Tertiary)]
    [InlineData("CreateStudentAccountCommand",ButtonKind.Secondary)]
    [InlineData("CorrectCommand",ButtonKind.Secondary)] [InlineData("TransferCommand",ButtonKind.Secondary)]
    public void ActionIntentSelectsPresentationWithoutChangingCommand(string command,ButtonKind expected)
    {Assert.Equal(expected,ButtonPresentation.ForCommand(command));}
    [Fact] public void NewAuxiliaryCommandsAreNeutralByDefault()
    {Assert.Equal(ButtonKind.Secondary,ButtonPresentation.ForCommand("UnknownAuxiliaryCommand"));}
    [Theory]
    [InlineData(ButtonKind.Primary,"PrimaryButton")]
    [InlineData(ButtonKind.Secondary,"SecondaryButton")]
    [InlineData(ButtonKind.Danger,"DangerButton")]
    [InlineData(ButtonKind.Tertiary,"TertiaryButton")]
    [InlineData(ButtonKind.Icon,"IconButton")]
    public void EveryIntentResolvesItsSharedStyle(ButtonKind kind,string resource)
        => Assert.Equal(resource,ButtonPresentation.Style(kind));
}
