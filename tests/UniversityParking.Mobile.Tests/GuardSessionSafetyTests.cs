using System.Net;
using System.Net.Http.Json;
using UniversityParking.Contracts.Parking;
using UniversityParking.Mobile.Core;
using UniversityParking.Mobile.ViewModels;

namespace UniversityParking.Mobile.Tests;

public sealed partial class GuardFeatureTests
{
    private sealed class AppNavigation : IAppNavigation
    {
        public Task ShowLoginAsync(string? message=null)=>Task.CompletedTask;
        public Task ShowAuthenticatedAsync()=>Task.CompletedTask;
    }
    [Fact] public async Task SessionChangesClearVisibleEvidenceWithoutAnotherScan()
    {
        var setup=await AccessSetup(_=>Task.FromResult(Ok(Ready())));await setup.Vm.ScanAsync("QR");
        Assert.NotNull(setup.Vm.Evidence);await setup.Session.SaveAsync("replacement",setup.Session.User! with{Id=Guid.NewGuid()});
        await Task.Yield();Assert.Null(setup.Vm.Evidence);Assert.Null(setup.Vm.Access);Assert.False(setup.Vm.CameraMounted);Assert.False(setup.Vm.CanConfirm);
    }
    [Theory] [InlineData(false)] [InlineData(true)]
    public async Task TransportNeverSendsMutationWithDifferentGuardSession(bool matches)
    {
        var session=new AuthSession(new Storage());var profile=new UniversityParking.Contracts.Users.UserProfileResponse(Guid.NewGuid(),"123","Guard",Guid.NewGuid(),"U",null,"STAFF","CARD","ACTIVE",["GUARD"]);
        await session.SaveAsync("current-session",profile);var calls=0;
        using var client=new HttpClient(new AuthHttpHandler(session,new AppNavigation(),new ApiOptions("https://test.example/"))
        {InnerHandler=new Transport(r=>{calls++;Assert.Equal("Bearer current-session",r.Headers.Authorization!.ToString());Assert.DoesNotContain(r.Headers,x=>x.Key.Contains("Expected",StringComparison.OrdinalIgnoreCase));return Task.FromResult(Ok(Movement()));})}) {BaseAddress=new("https://test.example/")};
        var result=await new ApiClient(client).PostForSessionAsync<ParkingMovementResponse>("api/v1/parking/check-in",new CheckInVehicleRequest(UserId,VehicleId,LotId),matches?"current-session":"old-session");
        Assert.Equal(matches,result.IsSuccess);Assert.Equal(matches?1:0,calls);Assert.Equal("current-session",await session.GetTokenAsync());
        if(!matches)Assert.Equal("SESSION_CHANGED",result.Error!.Code);
    }
    [Theory] [InlineData(false)] [InlineData(true)]
    public async Task UnknownExitOrWrongVerificationCannotRepeatMutation(bool wrongMovement)
    {
        var open=Movement();var posts=0;
        var setup=await AccessSetup(r=>
        {
            if(r.RequestUri!.AbsolutePath.EndsWith("lookup"))return Task.FromResult(Ok(Ready(open)));
            if(r.Method==HttpMethod.Get)return Task.FromResult(wrongMovement?Ok(open with{MovementId=Guid.NewGuid(),Status="CLOSED",CheckOutAtUtc=DateTimeOffset.UtcNow}):new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
            posts++;throw new TaskCanceledException();
        });
        await setup.Vm.ScanAsync("QR");await setup.Vm.ConfirmCommand.ExecuteAsync(null);Assert.True(setup.Vm.NeedsVerification);Assert.False(setup.Vm.CanConfirm);Assert.False(setup.Vm.ShowSuccess);
        await setup.Vm.ConfirmCommand.ExecuteAsync(null);Assert.Equal(1,posts);Assert.True(setup.Vm.CameraMounted);Assert.False(setup.Vm.IsDetecting);
    }
    [Theory] [InlineData("CLOSED")] [InlineData("OPEN")]
    public async Task InsideUsesSameFlowAndNeverPostsWhenObservedMovementClosed(string status)
    {
        var observed=Movement();var posts=0;
        var setup=await AccessSetup(r=>{if(r.Method==HttpMethod.Post)posts++;return Task.FromResult(Ok(r.RequestUri!.AbsolutePath.EndsWith("/vehicle")?(object)Evidenced():observed with{Status=status,CheckOutAtUtc=status=="CLOSED"?DateTimeOffset.UtcNow:null}));});
        await setup.Vm.OpenMovementAsync(observed);
        Assert.Equal(status=="OPEN",setup.Vm.CanConfirm);Assert.Equal(status=="OPEN",setup.Vm.ShowExit);Assert.False(setup.Vm.ShowEntry);Assert.Equal(0,posts);
        if(status=="CLOSED")Assert.Equal(GuardAccessState.ERROR,setup.Vm.State);else Assert.Equal(observed.MovementId,setup.Vm.Movement!.MovementId);
    }
    [Fact] public async Task RepeatedFailedQrRemainsOnPageAndCanResumeScanner()
    {
        var setup=await AccessSetup(_=>Task.FromResult(new HttpResponseMessage(HttpStatusCode.BadRequest)
        {Content=JsonContent.Create(new UniversityParking.Contracts.Common.ApiProblemDetails{Code="INVALID_QR_IDENTITY",Detail="El código QR no contiene una identificación válida."})}));
        await setup.Vm.ScanAsync("invalid");Assert.Equal(GuardAccessState.ERROR,setup.Vm.State);Assert.Contains("identificación válida",setup.Vm.ErrorMessage);Assert.True(setup.Vm.CameraMounted);
        setup.Vm.CancelCommand.Execute(null);Assert.True(setup.Vm.IsDetecting);await setup.Vm.ScanAsync("again");Assert.Equal(GuardAccessState.ERROR,setup.Vm.State);
    }
    [Theory] [InlineData(false)] [InlineData(true)]
    public async Task NativeRenderFailureBlocksOnlyEntryAndLateRenderCannotRestoreOldContext(bool exiting)
    {
        var setup=await AccessSetup(_=>Task.FromResult(Ok(Ready(exiting?Movement():null))));await setup.Vm.ScanAsync("QR");
        var bytes=setup.Vm.Evidence!;setup.Vm.ReportEvidenceRendered(bytes,false);
        Assert.Equal(exiting,setup.Vm.CanConfirm);Assert.Contains("evidencia",setup.Vm.ErrorMessage,StringComparison.OrdinalIgnoreCase);
        setup.Vm.CancelCommand.Execute(null);setup.Vm.ReportEvidenceRendered(bytes,true);Assert.False(setup.Vm.EvidenceRendered);Assert.True(setup.Vm.IsDetecting);
    }
}
