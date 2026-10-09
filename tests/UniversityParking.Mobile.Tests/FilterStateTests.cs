using UniversityParking.Mobile.Core;
namespace UniversityParking.Mobile.Tests;
public sealed class FilterStateTests
{
    [Fact] public void DraftDoesNotChangeAppliedUntilApply()
    {
        var state=new FilterState(new(){["Search"]="",["Status"]=""});state.Open();state.DraftFilters["Search"]="test";
        Assert.Equal("",state.AppliedFilters["Search"]);Assert.False(state.CanClear);state.Apply();Assert.Equal("test",state.AppliedFilters["Search"]);Assert.Equal(1,state.ActiveCount);
        state.Open();Assert.Equal("test",state.DraftFilters["Search"]);state.Clear();Assert.False(state.CanClear);Assert.Equal("",state.DraftFilters["Search"]);
    }
    [Fact] public void CountIncludesEachAppliedField()
    {var state=new FilterState(new(){["Search"]="",["Status"]=""});state.DraftFilters["Search"]="test";state.DraftFilters["Status"]="ACTIVE";state.Apply();Assert.Equal(2,state.ActiveCount);}
    [Theory][InlineData("STUDENT",false)][InlineData("TEACHER",true)]
    public void ScooterIsAllowedWithCorrectVehicleMatrix(string member,bool car)
    {var types=VehiclePresentation.AllowedTypes(member);Assert.Contains("SCOOTER",types);Assert.Equal(car,types.Contains("CAR"));Assert.Equal("Foto del scooter",VehiclePresentation.VerificationLabel("SCOOTER"));}
}
