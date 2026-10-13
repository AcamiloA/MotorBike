using UniversityParking.Mobile.Core;
namespace UniversityParking.Mobile.Tests;
public sealed class KeyboardBackStateTests
{
    [Fact] public void ClosedKeyboardDoesNotConsumeBack()
    {var state=new KeyboardBackState();Assert.False(state.ShouldDismiss());Assert.False(state.Update(false));}
    [Fact] public void FirstBackDismissesAndFollowingBackNavigates()
    {var state=new KeyboardBackState();state.Update(true);Assert.True(state.ShouldDismiss());Assert.True(state.Update(false));Assert.False(state.ShouldDismiss());Assert.False(state.Update(false));}
    [Fact] public void KeyboardHasPriorityRegardlessOfNavigationSurfaceAndRepeatedSessionsWork()
    {var state=new KeyboardBackState();for(var i=0;i<3;i++){state.Update(true);Assert.True(state.ShouldDismiss());Assert.True(state.Update(false));Assert.False(state.ShouldDismiss());}}
}
