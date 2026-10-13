using UniversityParking.Contracts.Common;
using UniversityParking.Contracts.Parking;
using UniversityParking.Mobile.Core;
using UniversityParking.Mobile.ViewModels;
namespace UniversityParking.Mobile.Tests;
public sealed partial class UserFeatureTests
{
    [Fact] public async Task HistoryGestureRetainsActualFiltersAndLoadsOnlyCurrentList()
    {
        var requests=new List<string>();var gate=new TaskCompletionSource();
        var service=Service(async request=>{requests.Add(request.RequestUri!.PathAndQuery);await gate.Task;return Ok(new PagedResponse<ParkingMovementResponse>([],1,20,0,0));});
        var selected=new VehicleFilter(Guid.NewGuid(),"Vehículo");
        var vm=new MyHistoryViewModel(service.Api){DateFrom=new(2026,10,1),DateTo=new(2026,10,7),SelectedVehicle=selected};
        var runner=new PullRefreshRunner();
        var first=runner.RunAsync(()=>vm.RefreshCommand.ExecuteAsync(null),()=>vm.IsBusy);
        Assert.True(vm.IsBusy);Assert.False(await runner.RunAsync(()=>vm.RefreshCommand.ExecuteAsync(null),()=>vm.IsBusy));Assert.Single(requests);
        gate.SetResult();await first;
        Assert.False(vm.IsBusy);Assert.False(runner.IsRefreshing);Assert.Same(selected,vm.SelectedVehicle);Assert.Equal(new DateTime(2026,10,1),vm.DateFrom);Assert.Equal(new DateTime(2026,10,7),vm.DateTo);
        await runner.RunAsync(()=>vm.RefreshCommand.ExecuteAsync(null),()=>vm.IsBusy);
        Assert.Equal(2,requests.Count);Assert.Equal(requests[0],requests[1]);Assert.Contains(selected.Id!.Value.ToString(),requests[0]);Assert.All(requests,x=>Assert.StartsWith("/api/v1/parking/history/me",x));
    }
}
