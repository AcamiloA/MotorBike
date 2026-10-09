using System.Net;
using System.Net.Http.Json;
using UniversityParking.Contracts.Auth;
using UniversityParking.Contracts.Common;
using UniversityParking.Mobile.Core;
using UniversityParking.Mobile.ViewModels;

namespace UniversityParking.Mobile.Tests;

public sealed partial class MobileFoundationTests
{
    private sealed class InputRelease(Func<Task> release) : ILoginInputLifecycle
    {
        public int Calls;
        public Task PrepareAsync(){Calls++;return release();}
    }
    [Fact] public async Task LoginReleasesFocusBeforeDisablingInputsAndClaimsOnlyOneOperation()
    {
        var session=new AuthSession(new Storage());var nav=new Navigation();var user=User();
        var transport=new Handler((r,_)=>Task.FromResult(r.Method==HttpMethod.Post?Ok(new LoginResponse("token",DateTimeOffset.UtcNow.AddHours(1),new(user.Id,user.FullName,user.MemberType,user.Roles))):Ok(user)));
        var gate=new TaskCompletionSource();var release=new InputRelease(()=>gate.Task);
        var vm=new LoginViewModel(new AuthService(Client(session,nav,transport),session,nav),inputs:release){IdentificationNumber="123",Password="Password1"};
        var first=vm.SignInCommand.ExecuteAsync(null);
        Assert.False(vm.IsBusy);Assert.False(vm.CanStart);Assert.Equal(0,transport.Count);
        await vm.SignInCommand.ExecuteAsync(null);await vm.CreateStudentAccountCommand.ExecuteAsync(null);Assert.Equal(1,release.Calls);
        gate.SetResult();await first;Assert.False(vm.IsBusy);Assert.True(vm.CanStart);Assert.Equal(1,nav.HomeCount);Assert.Equal(2,transport.Count);Assert.Empty(vm.Password);
    }
    [Fact] public async Task FailedLoginRestoresControlsAndCanRetrySuccessfully()
    {
        var session=new AuthSession(new Storage());var nav=new Navigation();var user=User();var fail=true;
        var transport=new Handler((r,_)=>Task.FromResult(r.Method==HttpMethod.Get?Ok(user):fail?new HttpResponseMessage(HttpStatusCode.Unauthorized){Content=JsonContent.Create(new ApiProblemDetails{Code="AUTH_INVALID_CREDENTIALS"})}:Ok(new LoginResponse("token",DateTimeOffset.UtcNow.AddHours(1),new(user.Id,user.FullName,user.MemberType,user.Roles)))));
        var release=new InputRelease(()=>Task.CompletedTask);var vm=new LoginViewModel(new AuthService(Client(session,nav,transport),session,nav),inputs:release){IdentificationNumber="123",Password="Password1"};
        await vm.SignInCommand.ExecuteAsync(null);Assert.True(vm.IsNotBusy);Assert.True(vm.CanStart);Assert.NotEmpty(vm.ErrorMessage);Assert.Empty(vm.Password);
        fail=false;vm.Password="Password1";await vm.SignInCommand.ExecuteAsync(null);Assert.Equal(2,release.Calls);Assert.Equal(1,nav.HomeCount);Assert.True(vm.CanStart);Assert.Empty(vm.ErrorMessage);
    }
    [Fact] public async Task InputPreparationFailureNeverPermanentlyLocksCommands()
    {
        var session=new AuthSession(new Storage());var nav=new Navigation();var transport=new Handler((_,_)=>throw new InvalidOperationException());
        var vm=new LoginViewModel(new AuthService(Client(session,nav,transport),session,nav),inputs:new InputRelease(()=>throw new InvalidOperationException())){IdentificationNumber="123",Password="Password1"};
        await vm.SignInCommand.ExecuteAsync(null);Assert.True(vm.CanStart);Assert.True(vm.IsNotBusy);Assert.Equal(0,transport.Count);Assert.Empty(vm.Password);
        vm.Password="Password1";await vm.SignInCommand.ExecuteAsync(null);Assert.True(vm.CanStart);Assert.Equal(0,transport.Count);
    }
    [Fact] public async Task OldLoginPageCannotDetachNewPageFocusHandler()
    {
        var coordinator=new LoginInputCoordinator();var oldPage=new object();var currentPage=new object();var calls=0;
        coordinator.Attach(oldPage,()=>throw new InvalidOperationException());coordinator.Attach(currentPage,()=>{calls++;return Task.CompletedTask;});
        coordinator.Detach(oldPage);await coordinator.PrepareAsync();Assert.Equal(1,calls);
        coordinator.Detach(currentPage);await coordinator.PrepareAsync();Assert.Equal(1,calls);
    }
}
