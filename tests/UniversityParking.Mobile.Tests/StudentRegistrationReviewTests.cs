using System.Net;
using System.Net.Http.Json;
using UniversityParking.Contracts.Common;
using UniversityParking.Contracts.Users;
using UniversityParking.Contracts.Vehicles;
using UniversityParking.Mobile.Core;
using UniversityParking.Mobile.ViewModels;

namespace UniversityParking.Mobile.Tests;

public sealed class StudentRegistrationReviewTests
{
    private static UserProfileResponse Profile(string status="PENDING")=>new(Guid.NewGuid(),"001","Nombre",Guid.NewGuid(),"Universidad","Carrera","STUDENT","CARD",status,["USER"]);
    private static async Task<(AdminUserDetailViewModel Vm,Nav Navigation,Transport Http)> Setup(bool confirm=true,bool timeout=false,string role="ADMIN")
    {
        var profile=Profile();var session=new AuthSession(new Storage());await session.SaveAsync("token",Profile("ACTIVE") with{Roles=["USER",role]});var nav=new Nav{Confirm=confirm};
        var http=new Transport(r=>
        {
            if(r.Method==HttpMethod.Patch){if(timeout)throw new TaskCanceledException();profile=profile with{Status=r.RequestUri!.AbsolutePath.EndsWith("approve")?"ACTIVE":"REJECTED"};return new(HttpStatusCode.NoContent);}
            return new(HttpStatusCode.OK){Content=r.RequestUri!.AbsolutePath.Contains("vehicles")?JsonContent.Create(new PagedResponse<VehicleResponse>([],1,20,0,0)):JsonContent.Create(profile)};
        });
        return(new(new AdminApiService(new ApiClient(new HttpClient(http){BaseAddress=new("https://test.example/")})),session,nav,new AppNav()){UserId=profile.Id,User=profile},nav,http);
    }
    [Theory][InlineData(true)][InlineData(false)]
    public async Task ConfirmedReviewUsesSemanticPatchAndReloadsState(bool approve)
    {
        var (vm,nav,http)=await Setup();Assert.True(vm.CanReviewRegistration);Assert.False(vm.ShowOperationalStatus);
        await (approve?vm.ApproveRegistrationCommand:vm.RejectRegistrationCommand).ExecuteAsync(null);
        Assert.Equal(1,http.Writes);Assert.EndsWith(approve?"/registration/approve":"/registration/reject",http.Path);
        Assert.Equal(approve?"ACTIVE":"REJECTED",vm.User!.Status);Assert.False(vm.IsPendingRegistration);Assert.False(vm.CanReviewRegistration);Assert.True(nav.Asked);
    }
    [Fact] public async Task CanceledReviewDoesNotWrite()
    {var (vm,_,http)=await Setup(confirm:false);await vm.ApproveRegistrationCommand.ExecuteAsync(null);Assert.Equal(0,http.Writes);Assert.Equal("PENDING",vm.User!.Status);}
    [Fact] public async Task UncertainReviewIsNotRepeatedUntilRefresh()
    {var (vm,_,http)=await Setup(timeout:true);await vm.RejectRegistrationCommand.ExecuteAsync(null);Assert.True(vm.WriteUncertain);Assert.False(vm.CanReviewRegistration);await vm.RejectRegistrationCommand.ExecuteAsync(null);Assert.Equal(1,http.Writes);}
    [Theory][InlineData("ACTIVE")][InlineData("INACTIVE")][InlineData("REJECTED")]
    public async Task ReviewHiddenAndBlockedForNonPendingStates(string state)
    {var (vm,_,http)=await Setup();vm.User=vm.User! with{Status=state};await vm.ApproveRegistrationCommand.ExecuteAsync(null);Assert.Equal(0,http.Writes);Assert.False(vm.IsPendingRegistration);Assert.Equal(state is "ACTIVE" or "INACTIVE",vm.ShowOperationalStatus);}
    [Fact] public async Task GenericActivationCannotApprovePending()
    {var (vm,_,http)=await Setup();await vm.StatusCommand.ExecuteAsync(null);Assert.Equal(0,http.Writes);}
    [Theory][InlineData("USER")][InlineData("GUARD")]
    public async Task NonAdminCannotSubmitReview(string role)
    {var (vm,_,http)=await Setup(role:role);await vm.ApproveRegistrationCommand.ExecuteAsync(null);Assert.Equal(0,http.Writes);Assert.Contains("ADMIN",vm.ErrorMessage);}
    [Fact] public void UserLabelsAreDistinctWithoutExpandingVehicleFilters()
    {Assert.Equal("Pendiente",AdminPresentation.Status("PENDING"));Assert.Equal("Rechazado",AdminPresentation.Status("REJECTED"));Assert.Equal(4,AdminPresentation.UserStatuses.Length);Assert.Equal(2,AdminPresentation.Statuses.Length);}
    private sealed class Transport(Func<HttpRequestMessage,HttpResponseMessage> send):HttpMessageHandler
    {public int Writes;public string? Path;protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken token){if(request.Method==HttpMethod.Patch){Writes++;Path=request.RequestUri!.AbsolutePath;}return Task.FromResult(send(request));}}
    private sealed class Storage:ISecretStorage{public Task<string?> GetAsync(string key)=>Task.FromResult<string?>(null);public Task SetAsync(string key,string value)=>Task.CompletedTask;public void Remove(string key){}}
    private sealed class Nav:IUserNavigation{public bool Confirm,Asked;public Task GoAsync(string route,IReadOnlyDictionary<string,object>? arguments=null)=>Task.CompletedTask;public Task BackAsync()=>Task.CompletedTask;public Task MessageAsync(string title,string message)=>Task.CompletedTask;public Task<bool> ConfirmAsync(string title,string message){Asked=true;return Task.FromResult(Confirm);}}
    private sealed class AppNav:IAppNavigation{public Task ShowLoginAsync(string? message=null)=>Task.CompletedTask;public Task ShowAuthenticatedAsync()=>Task.CompletedTask;}
}
