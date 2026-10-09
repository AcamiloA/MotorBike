using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using UniversityParking.Contracts.Auth;
using UniversityParking.Contracts.Universities;
using UniversityParking.Mobile.Core;
using UniversityParking.Mobile.ViewModels;

namespace UniversityParking.Mobile.Tests;

public sealed class RegisterStudentTests
{
    private static readonly UniversityResponse University=new(Guid.NewGuid(),"REMOTE","Nombre remoto");
    private static HttpResponseMessage Ok(object value)=>new(HttpStatusCode.OK){Content=JsonContent.Create(value)};
    private static (RegisterStudentViewModel Vm,Nav Nav,Http Http) Setup(Func<HttpRequestMessage,Task<HttpResponseMessage>>? send=null)
    {
        var http=new Http(send??(r=>Task.FromResult(r.Method==HttpMethod.Get?Ok(new[]{University}):new(HttpStatusCode.ServiceUnavailable))));var nav=new Nav();
        var vm=new RegisterStudentViewModel(new(new ApiClient(new HttpClient(http){BaseAddress=new("https://test.example/")})),nav)
        {IdentificationNumber=" 001 ",FullName=" Nombre ",Career=" Carrera ",CardCode=" CARD ",Password="Password1",ConfirmPassword="Password1"};return(vm,nav,http);
    }
    private static async Task Ready(RegisterStudentViewModel vm){await vm.LoadCommand.ExecuteAsync(null);vm.SelectedUniversity=vm.Universities.Single();}
    [Theory][InlineData("ACTIVE","Tu cuenta fue creada correctamente. Ya puedes iniciar sesión.")][InlineData("PENDING","Tu registro fue recibido y está pendiente de aprobación.")]
    public async Task SuccessUsesOnlyContractAndReturnsToLogin(string state,string message)
    {
        string? body=null;var (vm,nav,http)=Setup(async r=>{if(r.Method==HttpMethod.Get)return Ok(new[]{University});body=await r.Content!.ReadAsStringAsync();return new(HttpStatusCode.Created){Content=JsonContent.Create(new RegisterStudentResponse(Guid.NewGuid(),state))};});
        await Ready(vm);await vm.SubmitCommand.ExecuteAsync(null);Assert.True(vm.Completed);Assert.Equal(message,nav.Message);Assert.Equal(1,nav.Returns);Assert.Equal(1,http.Writes);
        Assert.Empty(vm.Password);Assert.Empty(vm.ConfirmPassword);Assert.False(vm.CanSubmit);
        using var json=JsonDocument.Parse(body!);Assert.Equal(University.Id,json.RootElement.GetProperty("universityId").GetGuid());Assert.Equal("001",json.RootElement.GetProperty("identificationNumber").GetString());
        Assert.Equal(new[]{"cardCode","career","fullName","identificationNumber","password","universityId"},json.RootElement.EnumerateObject().Select(x=>x.Name).Order());
    }
    [Fact] public async Task RequiresExplicitRemoteSelection()
    {var (vm,_,http)=Setup();await vm.LoadCommand.ExecuteAsync(null);Assert.Equal("Nombre remoto",Assert.Single(vm.Universities).Name);Assert.Null(vm.SelectedUniversity);Assert.False(vm.CanSubmit);await vm.SubmitCommand.ExecuteAsync(null);Assert.Contains("Selecciona",vm.ErrorMessage);Assert.Equal(0,http.Writes);}
    [Fact] public async Task ConfirmationMismatchPreventsSubmission()
    {var (vm,_,http)=Setup();await Ready(vm);vm.ConfirmPassword="Different1";await vm.SubmitCommand.ExecuteAsync(null);Assert.Equal("Las contraseñas no coinciden.",vm.ErrorMessage);Assert.Equal(0,http.Writes);}
    [Theory][InlineData("short")][InlineData("lowercase1")][InlineData("UPPERCASE1")][InlineData("NoNumbersHere")]
    public async Task ExistingPasswordRulesAreRequired(string password)
    {var (vm,_,http)=Setup();await Ready(vm);vm.Password=vm.ConfirmPassword=password;await vm.SubmitCommand.ExecuteAsync(null);Assert.Contains("contraseña",vm.ErrorMessage);Assert.Equal(0,http.Writes);}
    [Fact] public async Task FailedCatalogCanBeRetriedWithoutWrite()
    {
        var attempts=0;var (vm,_,http)=Setup(_=>Task.FromResult(++attempts<=2?new HttpResponseMessage(HttpStatusCode.ServiceUnavailable):Ok(new[]{University})));
        await vm.LoadCommand.ExecuteAsync(null);Assert.False(vm.CatalogLoaded);Assert.NotEmpty(vm.ErrorMessage);await vm.SubmitCommand.ExecuteAsync(null);Assert.Equal(0,http.Writes);
        await vm.LoadCommand.ExecuteAsync(null);Assert.True(vm.CatalogLoaded);Assert.Empty(vm.ErrorMessage);Assert.Null(vm.SelectedUniversity);
    }
    [Fact] public async Task DoubleSubmitIsBlocked()
    {
        var pending=new TaskCompletionSource<HttpResponseMessage>();var (vm,_,http)=Setup(r=>r.Method==HttpMethod.Get?Task.FromResult(Ok(new[]{University})):pending.Task);
        await Ready(vm);var first=vm.SubmitCommand.ExecuteAsync(null);Assert.False(vm.CanSubmit);await vm.SubmitCommand.ExecuteAsync(null);Assert.Equal(1,http.Writes);
        pending.SetResult(new(HttpStatusCode.Created){Content=JsonContent.Create(new RegisterStudentResponse(Guid.NewGuid(),"PENDING"))});await first;Assert.Empty(vm.Password);Assert.Empty(vm.ConfirmPassword);
    }
    [Fact] public async Task UncertainWriteCannotBeRepeatedOrUnlockedByCatalogRetry()
    {
        var (vm,nav,http)=Setup(r=>{if(r.Method==HttpMethod.Get)return Task.FromResult(Ok(new[]{University}));throw new TaskCanceledException();});
        await Ready(vm);await vm.SubmitCommand.ExecuteAsync(null);Assert.True(vm.WriteUncertain);Assert.Empty(vm.Password);Assert.Empty(vm.ConfirmPassword);Assert.Equal(0,nav.Returns);
        await vm.LoadCommand.ExecuteAsync(null);await vm.SubmitCommand.ExecuteAsync(null);Assert.Equal(1,http.Writes);
    }
    [Fact] public async Task LeaveDiscardsLateCatalogAndClearsSecrets()
    {
        var pending=new TaskCompletionSource<HttpResponseMessage>();var (vm,_,_)=Setup(_=>pending.Task);var load=vm.LoadCommand.ExecuteAsync(null);vm.Leave();
        pending.SetResult(Ok(new[]{University}));await load;Assert.Empty(vm.Universities);Assert.False(vm.CatalogLoaded);Assert.Empty(vm.Password);Assert.Empty(vm.ConfirmPassword);
    }
    [Fact] public async Task BackReturnsWithoutRegistering()
    {var (vm,nav,http)=Setup();await vm.BackCommand.ExecuteAsync(null);Assert.Equal(1,nav.Returns);Assert.Null(nav.Message);Assert.Empty(vm.Password);Assert.Empty(vm.ConfirmPassword);Assert.Equal(0,http.Writes);}
    [Fact] public async Task LoginSecondaryActionUsesPublicNavigationAndClearsPassword()
    {var nav=new Nav();var vm=new LoginViewModel(null!,nav){Password="Password1"};await vm.CreateStudentAccountCommand.ExecuteAsync(null);Assert.Equal(1,nav.Opened);Assert.Empty(vm.Password);}
    [Fact] public async Task InvalidServerSuccessLocksRetry()
    {
        var (vm,nav,http)=Setup(r=>Task.FromResult(r.Method==HttpMethod.Get?Ok(new[]{University}):Ok(new RegisterStudentResponse(Guid.NewGuid(),"REJECTED"))));
        await Ready(vm);await vm.SubmitCommand.ExecuteAsync(null);Assert.True(vm.WriteUncertain);Assert.Equal(0,nav.Returns);Assert.Empty(vm.Password);await vm.SubmitCommand.ExecuteAsync(null);Assert.Equal(1,http.Writes);
    }
    [Fact] public async Task EmptyCatalogKeepsSubmitDisabled()
    {var (vm,_,http)=Setup(_=>Task.FromResult(Ok(Array.Empty<UniversityResponse>())));await vm.LoadCommand.ExecuteAsync(null);Assert.False(vm.CanSubmit);Assert.Contains("No hay universidades",vm.ErrorMessage);await vm.SubmitCommand.ExecuteAsync(null);Assert.Equal(0,http.Writes);}
    [Fact] public async Task SpoofedSelectionCannotBeSubmitted()
    {var (vm,_,http)=Setup();await Ready(vm);vm.SelectedUniversity=new(Guid.NewGuid(),"OTHER","Otra");await vm.SubmitCommand.ExecuteAsync(null);Assert.Contains("Selecciona",vm.ErrorMessage);Assert.Equal(0,http.Writes);}
    [Fact] public async Task AllStudentFieldsAreRequiredBeforeRequest()
    {var (vm,_,http)=Setup();await Ready(vm);vm.Career=" ";await vm.SubmitCommand.ExecuteAsync(null);Assert.Contains("carrera",vm.ErrorMessage);Assert.Equal(0,http.Writes);}
    [Fact] public async Task LateRegistrationResponseCannotNavigateAfterLeaving()
    {
        var pending=new TaskCompletionSource<HttpResponseMessage>();var (vm,nav,http)=Setup(r=>r.Method==HttpMethod.Get?Task.FromResult(Ok(new[]{University})):pending.Task);
        await Ready(vm);var submit=vm.SubmitCommand.ExecuteAsync(null);vm.Leave();Assert.True(vm.WriteUncertain);
        pending.SetResult(new(HttpStatusCode.Created){Content=JsonContent.Create(new RegisterStudentResponse(Guid.NewGuid(),"ACTIVE"))});await submit;
        Assert.Equal(0,nav.Returns);Assert.False(vm.Completed);Assert.False(vm.CanSubmit);Assert.Equal(1,http.Writes);Assert.Empty(vm.Password);
    }
    private sealed class Nav:IPublicAuthNavigation{public int Returns,Opened;public string? Message;public Task ShowStudentRegistrationAsync(){Opened++;return Task.CompletedTask;}public Task ReturnToLoginAsync(string? message=null){Returns++;Message=message;return Task.CompletedTask;}}
    private sealed class Http(Func<HttpRequestMessage,Task<HttpResponseMessage>> send):HttpMessageHandler
    {public int Writes;protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken token){if(request.Method==HttpMethod.Post)Writes++;return send(request);}}
}
