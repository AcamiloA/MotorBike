using System.Net;
using System.Net.Http.Json;
using UniversityParking.Contracts.AcademicPeriods;
using UniversityParking.Contracts.Vehicles;
using UniversityParking.Mobile.ViewModels;

namespace UniversityParking.Mobile.Tests;

public sealed partial class UserFeatureTests
{
    [Theory][InlineData("CAR", "Frente de la Licencia de Tránsito")][InlineData("MOTORCYCLE", "Frente de la Licencia de Tránsito")][InlineData("BICYCLE", "Foto de la bicicleta")]
    public async Task VerificationLabelMatchesVehicleAndTypeChangeClearsSelectedImage(string type, string label)
    {
        var vm = new RegisterVehicleViewModel(Service(_ => throw new InvalidOperationException()).Api, await Session("STAFF"), new Picker(), new Navigation());
        vm.SelectedType = vm.Types.Single(x => x.Code == type); vm.VerificationImage = Photo;
        Assert.Equal(label, vm.VerificationLabel); Assert.DoesNotContain("GENERAL", vm.VerificationImageName);
        vm.SelectedType = vm.Types.Single(x => x.Code == (type == "BICYCLE" ? "MOTORCYCLE" : "BICYCLE")); Assert.Null(vm.VerificationImage);
    }
    [Fact]
    public async Task DetailDoesNotUseLegacyGeneralAsVerificationFallback()
    {
        var session = await Session(); var vehicle = Vehicle(session.User!.Id);
        var detail = new VehicleDetailResponse(vehicle, [new(Guid.NewGuid(), "GENERAL", "old.png", "image/png", 8, "/old/photo")], []);
        var paths = new List<string>();
        var service = Service(request => { paths.Add(request.RequestUri!.AbsolutePath); return Task.FromResult(
            request.RequestUri.AbsolutePath.Contains("academic-periods") ? Ok(new AcademicPeriodResponse(Guid.NewGuid(), "Periodo", new(2026, 1, 1), new(2026, 12, 31), "ACTIVE")) : Ok(detail)); });
        var vm = new VehicleDetailViewModel(service.Api, new Navigation(), new Viewer(), session) { VehicleId = vehicle.Id };
        await vm.LoadCommand.ExecuteAsync(null); Assert.Null(vm.Photo); Assert.Contains("pendiente", vm.VerificationPending);
        Assert.DoesNotContain("/old/photo", paths); Assert.True(vm.CanUpdateVerification);
    }
    [Fact]
    public async Task LegacyRenewalStopsBeforePostEvenWhenOldDocumentsExist()
    {
        var session = await Session(); var vehicle = Vehicle(session.User!.Id); var detail = new VehicleDetailResponse(vehicle, [], []);
        var methods = new List<HttpMethod>(); var service = Service(request => { methods.Add(request.Method); return Task.FromResult(Ok(detail)); });
        var vm = new RenewRegistrationViewModel(service.Api, new Navigation()) { VehicleId = vehicle.Id };
        await vm.LoadCommand.ExecuteAsync(null); Assert.Contains("pendiente", vm.ErrorMessage); await vm.SaveCommand.ExecuteAsync(null);
        Assert.DoesNotContain(HttpMethod.Post, methods);
    }
    [Fact]
    public async Task ReplacementSendsOnlyOneFileAndNoClientType()
    {
        string? body = null; HttpMethod? method = null;
        var service = Service(async request => { body = await request.Content!.ReadAsStringAsync(); method = request.Method; return new(HttpStatusCode.NoContent); });
        Assert.True((await service.Api.UpdateVerificationImageAsync(Guid.NewGuid(), Photo)).IsSuccess);
        Assert.Equal(HttpMethod.Put, method); Assert.Contains("VerificationImage.File", body);
        Assert.DoesNotContain("VerificationImage.Type", body); Assert.DoesNotContain("Documents[", body); Assert.DoesNotContain("Photos[", body);
    }
    [Fact]
    public async Task GuardOwnerDoesNotGetReplacementAction()
    {
        var session = await Session(); var profile = session.User! with { Roles = ["USER", "GUARD"] }; await session.SaveAsync("token", profile);
        var detail = new VehicleDetailResponse(Vehicle(profile.Id), [], []);
        var vm = new VerificationImageViewModel(Service(_ => Task.FromResult(Ok(detail))).Api, new Picker(), session, new Navigation()) { VehicleId = detail.Vehicle.Id };
        await vm.LoadCommand.ExecuteAsync(null); Assert.False(vm.CanEdit); await vm.PickCommand.ExecuteAsync(false); Assert.Null(vm.VerificationImage);
    }
}
