using UniversityParking.Mobile.Core;
namespace UniversityParking.Mobile.Tests;
public sealed class PasswordVisibilityStateTests
{
    [Fact] public void IndependentFieldsPreserveSelectionAndAccessibleState()
    {
        var first=new PasswordVisibilityState();var second=new PasswordVisibilityState();
        Assert.Equal((2,3),first.Toggle(2,3,8));Assert.True(first.Visible);Assert.False(second.Visible);
        Assert.Equal("Ocultar contraseña",first.Description);Assert.Equal("password_eye_off.png",first.Icon);
        Assert.Equal((2,3),first.Toggle(2,3,8));Assert.False(first.Visible);Assert.Equal("Mostrar contraseña",first.Description);
    }
    [Theory] [InlineData(20,10,8,8,0)] [InlineData(3,20,8,3,5)] [InlineData(-1,-3,0,0,0)]
    public void SelectionRemainsValidAfterNativeInputChanges(int cursor,int selection,int length,int expectedCursor,int expectedSelection)
    {Assert.Equal((expectedCursor,expectedSelection),new PasswordVisibilityState().Toggle(cursor,selection,length));}
}
