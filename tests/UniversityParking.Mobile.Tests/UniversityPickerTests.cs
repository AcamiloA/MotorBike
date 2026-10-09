using System.Net;
using System.Net.Http.Json;
using UniversityParking.Contracts.Universities;
using UniversityParking.Contracts.Users;
using UniversityParking.Mobile.Core;
using UniversityParking.Mobile.ViewModels;

namespace UniversityParking.Mobile.Tests;

public sealed class UniversityPickerTests
{
    private static readonly UniversityResponse Active = new(Guid.NewGuid(), "CMC", "Colegio Mayor de Cundinamarca");
    private static HttpResponseMessage Ok(object value) => new(HttpStatusCode.OK) { Content = JsonContent.Create(value) };
    private static UserProfileResponse Profile(Guid id, Guid university) => new(id,"123","Nombre",university,"ETITC","Carrera","STUDENT","CARD","ACTIVE",["USER","ADMIN"],Email:"student@example.com",PhoneNumber:"+573001234567");
    private static async Task<(AdminUserFormViewModel Vm, AuthSession Session, Transport Http)> Setup(Func<HttpRequestMessage,Task<HttpResponseMessage>> send)
    {
        var session=new AuthSession(new Storage());await session.SaveAsync("token",Profile(Guid.NewGuid(),Active.Id));
        var http=new Transport(send);var api=new AdminApiService(new ApiClient(new HttpClient(http){BaseAddress=new("https://test.example/")}));
        return(new(api,session,new Navigation()){Identification="123",FullName="Nombre",Career="Carrera",CardCode="CARD",InitialPassword="Password1",FirstName="Nombre",LastName="Apellido",Email="student@example.com",PhoneNumber="+573001234567",ConfirmPassword="Password1"},session,http);
    }

    [Fact] public async Task CreationRequiresExplicitSelectionEvenWithOnlyOneUniversity()
    {
        var (vm,_,http)=await Setup(_=>Task.FromResult(Ok(new[]{Active})));
        await vm.LoadCommand.ExecuteAsync(null);Assert.Single(vm.Universities);Assert.Null(vm.SelectedUniversity);Assert.False(vm.CanSave);
        await vm.SaveCommand.ExecuteAsync(null);Assert.Contains("Selecciona",vm.ErrorMessage);Assert.Equal(1,http.Calls);
        vm.SelectedUniversity=vm.Universities.Single();Assert.True(vm.CanSave);Assert.Equal(Active.Id,vm.UniversityId);Assert.Equal(Active.Name,vm.UniversityName);
    }

    [Theory][InlineData(true)][InlineData(false)]
    public async Task EditSelectsCurrentReferenceAndPreservesInactiveCurrent(bool active)
    {
        var id=Guid.NewGuid();var current=active?Active.Id:Guid.NewGuid();
        var (vm,_,http)=await Setup(r=>Task.FromResult(r.Method==HttpMethod.Put?new HttpResponseMessage(HttpStatusCode.NoContent):r.RequestUri!.AbsolutePath.EndsWith("universities")?Ok(new[]{Active}):Ok(Profile(id,current))));
        vm.UserId=id;await vm.LoadCommand.ExecuteAsync(null);
        Assert.Equal(current,vm.UniversityId);Assert.True(vm.CanSave);Assert.Equal(active?1:2,vm.Universities.Count);
        Assert.Equal(!active,!string.IsNullOrEmpty(vm.CurrentUniversityNotice));
        await vm.SaveCommand.ExecuteAsync(null);Assert.True(vm.Completed);Assert.Equal(3,http.Calls);
    }

    [Fact] public async Task FailedCatalogBlocksSaveAndCanBeRetried()
    {
        var attempt=0;var (vm,_,http)=await Setup(_=>Task.FromResult(++attempt<=2?new HttpResponseMessage(HttpStatusCode.ServiceUnavailable):Ok(new[]{Active})));
        await vm.LoadCommand.ExecuteAsync(null);Assert.False(vm.CanSave);Assert.False(vm.UniversitiesLoaded);Assert.NotEmpty(vm.ErrorMessage);
        await vm.SaveCommand.ExecuteAsync(null);Assert.Equal(2,http.Calls);
        await vm.LoadCommand.ExecuteAsync(null);Assert.Empty(vm.ErrorMessage);vm.SelectedUniversity=vm.Universities.Single();Assert.True(vm.CanSave);
    }

    [Fact] public async Task FailedProfileBlocksEditingEvenWhenCatalogSucceeded()
    {
        var (vm,_,http)=await Setup(r=>Task.FromResult(r.RequestUri!.AbsolutePath.EndsWith("universities")?Ok(new[]{Active}):new HttpResponseMessage(HttpStatusCode.NotFound)));
        vm.UserId=Guid.NewGuid();await vm.LoadCommand.ExecuteAsync(null);Assert.False(vm.CanSave);Assert.Empty(vm.Universities);
        await vm.SaveCommand.ExecuteAsync(null);Assert.Equal(2,http.Calls);
    }

    [Fact] public async Task InjectedSelectionOutsideCatalogCannotBeSubmitted()
    {
        var (vm,_,http)=await Setup(_=>Task.FromResult(Ok(new[]{Active})));await vm.LoadCommand.ExecuteAsync(null);
        vm.SelectedUniversity=new(Guid.NewGuid(),"OTHER","Inventada");Assert.False(vm.CanSave);
        await vm.SaveCommand.ExecuteAsync(null);Assert.Contains("Selecciona",vm.ErrorMessage);Assert.Equal(1,http.Calls);
    }

    [Fact] public async Task CatalogLoadingBlocksSelectionAndSubmission()
    {
        var pending=new TaskCompletionSource<HttpResponseMessage>();var (vm,_,http)=await Setup(_=>pending.Task);
        var loading=vm.LoadCommand.ExecuteAsync(null);Assert.True(vm.IsBusy);Assert.False(vm.CanSelectUniversity);Assert.False(vm.CanSave);
        await vm.SaveCommand.ExecuteAsync(null);Assert.Equal(1,http.Calls);pending.SetResult(Ok(new[]{Active}));await loading;
        Assert.True(vm.CanSelectUniversity);Assert.Null(vm.SelectedUniversity);
    }

    [Fact] public async Task SessionClearedDuringLoadingDiscardsLateCatalog()
    {
        var pending=new TaskCompletionSource<HttpResponseMessage>();var (vm,session,_)=await Setup(_=>pending.Task);
        var loading=vm.LoadCommand.ExecuteAsync(null);await session.ClearAsync();pending.SetResult(Ok(new[]{Active}));await loading;
        Assert.False(vm.UniversitiesLoaded);Assert.Empty(vm.Universities);Assert.False(vm.CanSave);
    }

    [Fact] public async Task ReloadDropsSelectionThatIsNoLongerActiveForCreation()
    {
        var attempt=0;var (vm,_,_)=await Setup(_=>Task.FromResult(Ok(++attempt==1?new[]{Active}:Array.Empty<UniversityResponse>())));
        await vm.LoadCommand.ExecuteAsync(null);vm.SelectedUniversity=vm.Universities.Single();await vm.LoadCommand.ExecuteAsync(null);
        Assert.Null(vm.SelectedUniversity);Assert.False(vm.CanSave);Assert.Contains("No hay universidades",vm.ErrorMessage);
    }

    [Theory][InlineData(true)][InlineData(false)]
    public async Task InvalidCatalogFailsWithoutOfferingInvalidOptions(bool duplicate)
    {
        var values=duplicate?new[]{Active,Active}:new[]{Active with{Id=Guid.Empty}};
        var (vm,_,_)=await Setup(_=>Task.FromResult(Ok(values)));await vm.LoadCommand.ExecuteAsync(null);
        Assert.False(vm.UniversitiesLoaded);Assert.Empty(vm.Universities);Assert.NotEmpty(vm.ErrorMessage);
    }

    private sealed class Transport(Func<HttpRequestMessage,Task<HttpResponseMessage>> send):HttpMessageHandler
    {public int Calls;protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken token){Calls++;return send(request);}}
    private sealed class Storage:ISecretStorage
    {public Task<string?> GetAsync(string key)=>Task.FromResult<string?>(null);public Task SetAsync(string key,string value)=>Task.CompletedTask;public void Remove(string key){}}
    private sealed class Navigation:IUserNavigation
    {public Task GoAsync(string route,IReadOnlyDictionary<string,object>? arguments=null)=>Task.CompletedTask;public Task BackAsync()=>Task.CompletedTask;public Task MessageAsync(string title,string message)=>Task.CompletedTask;public Task<bool> ConfirmAsync(string title,string message)=>Task.FromResult(true);}
}
