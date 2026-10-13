using UniversityParking.Mobile.Core;
namespace UniversityParking.Mobile.Tests;
public sealed class PullRefreshRunnerTests
{
    [Fact] public async Task ReloadKeepsFilterObjectAndPreventsConcurrentLoads()
    {
        var runner=new PullRefreshRunner();var filters=new Dictionary<string,string>{{"status","ACTIVE"}};var gate=new TaskCompletionSource();var calls=0;
        async Task Load(){calls++;Assert.Equal("ACTIVE",filters["status"]);await gate.Task;}
        var first=runner.RunAsync(Load,()=>false);Assert.True(runner.IsRefreshing);
        Assert.False(await runner.RunAsync(Load,()=>false));Assert.Equal(1,calls);
        gate.SetResult();Assert.True(await first);Assert.False(runner.IsRefreshing);Assert.Equal("ACTIVE",filters["status"]);
    }
    [Fact] public async Task BusyViewModelDoesNotStartAnotherLoad()
    {var runner=new PullRefreshRunner();var calls=0;Assert.False(await runner.RunAsync(()=>{calls++;return Task.CompletedTask;},()=>true));Assert.Equal(0,calls);Assert.False(runner.IsRefreshing);}
    [Theory] [InlineData(false)] [InlineData(true)]
    public async Task FailureOrCancellationAlwaysEndsLoadingAndAllowsNextGesture(bool canceled)
    {var runner=new PullRefreshRunner();await Assert.ThrowsAnyAsync<Exception>(()=>runner.RunAsync(()=>Task.FromException(canceled?new OperationCanceledException():new InvalidOperationException()),()=>false));Assert.False(runner.IsRefreshing);Assert.True(await runner.RunAsync(()=>Task.CompletedTask,()=>false));}
    [Fact] public void FailedAutomaticCatalogLoadRetriesOnReentryWithoutReloadingSuccessfulForms()
    {var state=new AutomaticPageLoadState();Assert.True(state.ShouldLoad(true,false));state.Complete(false);Assert.True(state.ShouldLoad(true,false));state.Complete(true);Assert.False(state.ShouldLoad(true,false));Assert.True(state.ShouldLoad(false,false));Assert.False(state.ShouldLoad(false,true));}
}
