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
    public void ActionIntentSelectsPresentationWithoutChangingCommand(string command,ButtonKind expected)
    {Assert.Equal(expected,ButtonPresentation.ForCommand(command));}
    [Fact] public void NewAuxiliaryCommandsAreNeutralByDefault()
    {Assert.Equal(ButtonKind.Secondary,ButtonPresentation.ForCommand("UnknownAuxiliaryCommand"));}
}
