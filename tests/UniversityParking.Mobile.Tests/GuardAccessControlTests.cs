using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using UniversityParking.Contracts.Parking;
using UniversityParking.Mobile.Core;
using UniversityParking.Mobile.ViewModels;

namespace UniversityParking.Mobile.Tests;

public sealed partial class GuardFeatureTests
{
    private static EligibleVehicleResponse Evidenced(Guid? id = null, string type = "BICYCLE") => new(id ?? VehicleId,type,"FRAME123","Brand","Model","Black",
        new(Guid.NewGuid(),type == "BICYCLE" ? "BICYCLE_PHOTO" : "TRANSIT_LICENSE_FRONT","/api/v1/vehicles/evidence/content"));
    private static ParkingAccessResponse Ready(ParkingMovementResponse? movement = null, params EligibleVehicleResponse[] vehicles) =>
        new(new(UserId,"Usuario","STUDENT","ACTIVE"),movement,movement is null ? vehicles.Length==0 ? [Evidenced()] : vehicles : [],movement is null ? null : Evidenced());
    private sealed class ImageValidator(bool valid = true) : IEvidenceImageValidator { public Task<bool> CanDisplayAsync(byte[] bytes) => Task.FromResult(valid); }
    private static async Task<(GuardAccessControlViewModel Vm,GuardLotSession Lots,AuthSession Session)> AccessSetup(
        Func<HttpRequestMessage,Task<HttpResponseMessage>> send, bool imageWorks = true, IScannerPermission? permission = null, params string[] roles)
    {
        var setup = await Setup(r => r.RequestUri!.AbsolutePath.EndsWith("parking-lots") ? Task.FromResult(LotPage(Lot())) :
            r.RequestUri.AbsolutePath.EndsWith("/content") ? Task.FromResult(new HttpResponseMessage(imageWorks ? HttpStatusCode.OK : HttpStatusCode.NotFound) { Content=new ByteArrayContent([1,2,3]) }) : send(r),roles);
        var vm = new GuardAccessControlViewModel(setup.Api,setup.Lots,setup.Session,permission ?? new Permission(true),new ImageValidator(imageWorks));
        // Simulate the view's successful native rendering notification; dedicated tests cover rejection.
        vm.PropertyChanged += (_, e) => { if (e.PropertyName == nameof(vm.Evidence) && vm.Evidence is { } bytes) vm.ReportEvidenceRendered(bytes, true); };
        await vm.StartCommand.ExecuteAsync(null); return (vm,setup.Lots,setup.Session);
    }
    [Fact] public async Task DuplicateScanClaimsOneLookupAndKeepsCameraMounted()
    {
        var pending = new TaskCompletionSource<HttpResponseMessage>(); var lookups=0; string? body=null;
        var setup=await AccessSetup(async r=>{lookups++;body=await r.Content!.ReadAsStringAsync();return await pending.Task;});
        Assert.True(setup.Vm.IsDetecting); var first=setup.Vm.ScanAsync("MTAxNDI4NTU1Mw==");
        await setup.Vm.ScanAsync("1014285553"); Assert.Equal(1,lookups); Assert.False(setup.Vm.IsDetecting); Assert.True(setup.Vm.CameraMounted);
        pending.SetResult(Ok(Ready())); await first;
        Assert.Contains("qrPayload",body); Assert.DoesNotContain("cardCode",body);
        Assert.Equal(GuardAccessState.CONFIRMING_ENTRY,setup.Vm.State); Assert.True(setup.Vm.CanConfirm); Assert.True(setup.Vm.CameraMounted); Assert.False(setup.Vm.ShowExit);
        setup.Vm.CancelCommand.Execute(null); Assert.True(setup.Vm.IsDetecting); Assert.Null(setup.Vm.Evidence); Assert.Null(setup.Vm.Access);
    }
    [Fact] public async Task LeavingOrChangingSessionSuppressesPendingLookup()
    {
        foreach(var leave in new[]{true,false})
        {
            var pending=new TaskCompletionSource<HttpResponseMessage>(); var setup=await AccessSetup(_=>pending.Task);
            var scan=setup.Vm.ScanAsync("QR"); if(leave) setup.Vm.Stop(); else await setup.Session.SaveAsync("different",setup.Session.User! with {Id=Guid.NewGuid()});
            pending.SetResult(Ok(Ready()));await scan; Assert.Null(setup.Vm.Access); Assert.False(setup.Vm.IsDetecting); Assert.False(setup.Vm.CameraMounted);
        }
    }
    [Fact] public async Task LotChangeClearsConfirmationAndIgnoresLateLookup()
    {
        var pending=new TaskCompletionSource<HttpResponseMessage>();var setup=await AccessSetup(_=>pending.Task);
        var scan=setup.Vm.ScanAsync("QR"); setup.Lots.Selected=Lot(Guid.NewGuid()); pending.SetResult(Ok(Ready())); await scan;
        Assert.Equal(GuardAccessState.SCANNING,setup.Vm.State);Assert.Null(setup.Vm.Access);Assert.False(setup.Vm.CanConfirm);Assert.True(setup.Vm.IsDetecting);
    }
    [Fact] public async Task DeniedCameraManualSearchAndCancelUseSamePersistentFlow()
    {
        var calls=0;string? body=null; var setup=await AccessSetup(async r=>{calls++;body=await r.Content!.ReadAsStringAsync();return Ok(Ready());},permission:new Permission(false));
        Assert.False(setup.Vm.CameraMounted);Assert.Contains("No se concedió",setup.Vm.ErrorMessage);
        setup.Vm.ManualCommand.Execute(null);Assert.Equal(GuardAccessState.MANUAL_LOOKUP,setup.Vm.State);
        await setup.Vm.SearchCommand.ExecuteAsync(null);Assert.Equal(0,calls);
        setup.Vm.IdentificationNumber=" 1014285553 "; await setup.Vm.SearchCommand.ExecuteAsync(null);
        Assert.Contains("1014285553",body); Assert.Equal(GuardAccessState.CONFIRMING_ENTRY,setup.Vm.State);
        setup.Vm.CancelCommand.Execute(null);Assert.Equal(GuardAccessState.SCANNING,setup.Vm.State);
    }
    [Fact] public async Task ManualCancelResumesScanner()
    {
        var setup=await AccessSetup(_=>throw new Exception()); setup.Vm.ManualCommand.Execute(null);Assert.False(setup.Vm.IsDetecting);
        setup.Vm.CancelCommand.Execute(null);Assert.True(setup.Vm.IsDetecting);Assert.True(setup.Vm.CameraMounted);
    }
    [Fact] public async Task SeveralVehiclesRequireExplicitVisualSelection()
    {
        var setup=await AccessSetup(_=>Task.FromResult(Ok(Ready(null,Evidenced(),Evidenced(Guid.NewGuid(),"MOTORCYCLE")))));
        await setup.Vm.ScanAsync("QR");Assert.Equal(GuardAccessState.SELECTING_VEHICLE,setup.Vm.State);Assert.Null(setup.Vm.SelectedVehicle);Assert.False(setup.Vm.CanConfirm);Assert.Equal(2,setup.Vm.Vehicles.Count);
        Assert.All(setup.Vm.Vehicles,x=>Assert.NotNull(x.Image));
        await setup.Vm.SelectCommand.ExecuteAsync(setup.Vm.Vehicles[1]);Assert.True(setup.Vm.ShowEntry);Assert.Contains("PLACA REGISTRADA",setup.Vm.VehicleSummary);Assert.Equal("Frente de la Licencia de Tránsito",setup.Vm.EvidenceLabel);
    }
    [Theory] [InlineData("PENDING")] [InlineData("REJECTED")] [InlineData("INACTIVE")]
    public async Task NonActiveUserWithoutOpenCannotEnter(string status)
    {
        var setup=await AccessSetup(_=>Task.FromResult(Ok(Ready() with {User=new(UserId,"Usuario","STUDENT",status)})));
        await setup.Vm.ScanAsync("QR"); Assert.Equal(GuardAccessState.ERROR,setup.Vm.State); Assert.False(setup.Vm.ShowEntry);Assert.False(setup.Vm.CanConfirm);
    }
    [Fact] public async Task ZeroVehiclesShowsControlledErrorAndCanScanAgain()
    {
        var setup=await AccessSetup(_=>Task.FromResult(Ok(new ParkingAccessResponse(new(UserId,"Usuario","STUDENT","ACTIVE"),null,[]))));
        await setup.Vm.ScanAsync("QR"); Assert.Contains("No hay vehículos",setup.Vm.ErrorMessage);setup.Vm.CancelCommand.Execute(null);Assert.True(setup.Vm.IsDetecting);
    }
    [Theory] [InlineData(false)] [InlineData(true)]
    public async Task MissingOrUnreadableImageBlocksEntryButNeverExit(bool exiting)
    {
        var open=Movement();var response=exiting?Ready(open):Ready();
        var setup=await AccessSetup(_=>Task.FromResult(Ok(response)),imageWorks:false);
        await setup.Vm.ScanAsync("QR");Assert.Equal(exiting,setup.Vm.CanConfirm);Assert.Equal(exiting,setup.Vm.ShowExit);Assert.Equal(!exiting,setup.Vm.ShowEntry);Assert.Null(setup.Vm.Evidence);
        Assert.Empty(setup.Vm.Vehicles);Assert.True(setup.Vm.CameraMounted);Assert.False(setup.Vm.IsDetecting);
    }
    [Fact] public async Task OpenUserCannotSelectOtherVehicleEvenIfResponseContainsOne()
    {
        var open=Movement();var setup=await AccessSetup(_=>Task.FromResult(Ok(Ready(open) with {EligibleVehicles=[Evidenced(Guid.NewGuid())],User=new(UserId,"Usuario","STUDENT","INACTIVE")})));
        await setup.Vm.ScanAsync("QR");Assert.True(setup.Vm.ShowExit);Assert.False(setup.Vm.ShowEntry);Assert.Empty(setup.Vm.Vehicles);Assert.Equal(open.VehicleId,setup.Vm.SelectedVehicle!.Id);
    }
    [Theory] [InlineData(false)] [InlineData(true)]
    public async Task InlineInspectionPreservesConfirmationAndPausedScanner(bool exiting)
    {
        var setup=await AccessSetup(_=>Task.FromResult(Ok(Ready(exiting?Movement():null))));await setup.Vm.ScanAsync("QR");var state=setup.Vm.State;
        var user=setup.Vm.Access;var vehicle=setup.Vm.SelectedVehicle;var movement=setup.Vm.Movement;var bytes=setup.Vm.Evidence;
        var viewport=new EvidenceViewport();viewport.TouchDown(150,130,300,260,300,260);
        setup.Vm.IsInspectingEvidence=viewport.Pressed;Assert.True(setup.Vm.CameraMounted);Assert.False(setup.Vm.IsDetecting);Assert.False(setup.Vm.CanConfirm);
        await setup.Vm.ScanAsync("1014285553");viewport.TouchMove(200,150);
        Assert.Same(user,setup.Vm.Access);Assert.Same(vehicle,setup.Vm.SelectedVehicle);Assert.Same(movement,setup.Vm.Movement);Assert.Same(bytes,setup.Vm.Evidence);
        viewport.TouchUp();setup.Vm.IsInspectingEvidence=viewport.Pressed;
        Assert.Equal(state,setup.Vm.State);Assert.False(setup.Vm.IsDetecting);Assert.True(setup.Vm.CanConfirm);Assert.Equal(1,viewport.Scale);
        setup.Vm.CancelCommand.Execute(null);Assert.True(setup.Vm.IsDetecting);
    }
    [Theory] [InlineData(false)] [InlineData(true)]
    public async Task SingleConfirmationDoubleTapSuccessAndContinue(bool exiting)
    {
        var posts=0;var pending=new TaskCompletionSource<HttpResponseMessage>();var open=Movement();CheckInVehicleRequest? entry=null;CheckOutVehicleRequest? exit=null;
        var setup=await AccessSetup(async r=>{if(r.RequestUri!.AbsolutePath.EndsWith("lookup"))return Ok(Ready(exiting?open:null));posts++;if(exiting)exit=await r.Content!.ReadFromJsonAsync<CheckOutVehicleRequest>();else entry=await r.Content!.ReadFromJsonAsync<CheckInVehicleRequest>();return await pending.Task;});
        await setup.Vm.ScanAsync("QR");var first=setup.Vm.ConfirmCommand.ExecuteAsync(null);await setup.Vm.ConfirmCommand.ExecuteAsync(null);
        Assert.Equal(1,posts);Assert.Equal(GuardAccessState.SUBMITTING,setup.Vm.State);Assert.False(setup.Vm.CanConfirm);Assert.True(setup.Vm.CameraMounted);
        var result=exiting?open with {Status="CLOSED",CheckOutAtUtc=DateTimeOffset.UtcNow}:Movement() with {MovementId=entry!.MovementId!.Value};
        pending.SetResult(Ok(result));await first;Assert.Equal(exiting?GuardAccessState.SUCCESS_EXIT:GuardAccessState.SUCCESS_ENTRY,setup.Vm.State);
        if(exiting){Assert.Equal(open.MovementId,exit!.MovementId);Assert.Equal(open.VehicleId,exit.VehicleId);}else Assert.NotEqual(Guid.Empty,entry!.MovementId);
        Assert.False(setup.Vm.IsDetecting);setup.Vm.ContinueCommand.Execute(null);Assert.True(setup.Vm.IsDetecting);Assert.Null(setup.Vm.Movement);Assert.Empty(setup.Vm.ResultMessage);
    }
    [Theory] [InlineData(false)] [InlineData(true)]
    public async Task UncertainMutationQueriesExactMovementWithoutRetry(bool exiting)
    {
        var posts=0;var open=Movement();Guid? expected=null;var setupSessionId=Guid.Empty;
        var setup=await AccessSetup(async r=>
        {
            if(r.RequestUri!.AbsolutePath.EndsWith("lookup"))return Ok(Ready(exiting?open:null));
            if(r.Method==HttpMethod.Get){Assert.EndsWith(expected.ToString(),r.RequestUri.AbsolutePath);return Ok(open with {MovementId=expected!.Value,Status=exiting?"CLOSED":"OPEN",CheckOutAtUtc=exiting?DateTimeOffset.UtcNow:null,CheckInGuardId=setupSessionId});}
            posts++;expected=exiting?open.MovementId:(await r.Content!.ReadFromJsonAsync<CheckInVehicleRequest>())!.MovementId;throw new TaskCanceledException();
        });
        setupSessionId=setup.Session.User!.Id;await setup.Vm.ScanAsync("QR");await setup.Vm.ConfirmCommand.ExecuteAsync(null);
        Assert.Equal(1,posts);Assert.True(setup.Vm.ShowSuccess);Assert.False(setup.Vm.NeedsVerification);await setup.Vm.ConfirmCommand.ExecuteAsync(null);Assert.Equal(1,posts);
    }
    [Fact] public async Task UnknownMutationRemainsLockedUntilExplicitExactVerification()
    {
        var posts=0;var recover=false;Guid? expected=null;Guid actor=Guid.Empty;
        var setup=await AccessSetup(async r=>
        {
            if(r.RequestUri!.AbsolutePath.EndsWith("lookup"))return Ok(Ready());
            if(r.Method==HttpMethod.Get)return recover?Ok(Movement() with{MovementId=expected!.Value,CheckInGuardId=actor}):new HttpResponseMessage(HttpStatusCode.ServiceUnavailable);
            posts++;expected=(await r.Content!.ReadFromJsonAsync<CheckInVehicleRequest>())!.MovementId;throw new TaskCanceledException();
        });
        actor=setup.Session.User!.Id;await setup.Vm.ScanAsync("QR");await setup.Vm.ConfirmCommand.ExecuteAsync(null);Assert.True(setup.Vm.NeedsVerification);Assert.False(setup.Vm.CanConfirm);
        setup.Vm.CancelCommand.Execute(null);setup.Vm.ManualCommand.Execute(null);await setup.Vm.ConfirmCommand.ExecuteAsync(null);Assert.Equal(1,posts);Assert.Contains("No fue posible confirmar",setup.Vm.ErrorMessage);
        recover=true;await setup.Vm.VerifyCommand.ExecuteAsync(null);Assert.True(setup.Vm.ShowSuccess);Assert.Equal(1,posts);
    }
    [Fact] public async Task PermissionCompletingAfterDepartureCannotMountCamera()
    {
        var pending=new TaskCompletionSource<bool>();var setup=await Setup(_=>Task.FromResult(LotPage(Lot())));
        var vm=new GuardAccessControlViewModel(setup.Api,setup.Lots,setup.Session,new PendingPermission(pending.Task),new ImageValidator());
        var start=vm.StartCommand.ExecuteAsync(null);vm.Stop();pending.SetResult(true);await start;Assert.False(vm.CameraMounted);Assert.False(vm.IsDetecting);
    }
    [Theory] [InlineData("USER")] [InlineData("ADMIN")]
    public async Task NonGuardCannotStartOperationalFlow(string role)
    {
        var setup=await AccessSetup(_=>throw new Exception(),roles:[role]);Assert.Contains("GUARD",setup.Vm.ErrorMessage);Assert.False(setup.Vm.CanConfirm);Assert.False(setup.Vm.CameraMounted);
    }
    [Fact] public void PressDragReleaseAreBoundedAndResettable()
    {
        var v=new EvidenceViewport();v.TouchDown(150,100,300,200,300,200);Assert.Equal(2.5,v.Scale);
        v.TouchMove(900,-900);Assert.Equal(225,v.X);Assert.Equal(-150,v.Y);
        v.TouchUp();Assert.Equal(1,v.Scale);Assert.Equal(0,v.X);Assert.Equal(0,v.Y);
    }
}
