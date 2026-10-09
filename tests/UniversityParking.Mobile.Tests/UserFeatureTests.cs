using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using UniversityParking.Contracts.AcademicPeriods;
using UniversityParking.Contracts.Common;
using UniversityParking.Contracts.Vehicles;
using UniversityParking.Contracts.Users;
using UniversityParking.Contracts.News;
using UniversityParking.Mobile.Core;
using UniversityParking.Mobile.ViewModels;

namespace UniversityParking.Mobile.Tests;

public sealed partial class UserFeatureTests
{
    private static PickedAttachment Photo => new("photo.png", "image/png", [137,80,78,71,13,10,26,10]);
    private static PickedAttachment Pdf => new("support.pdf", "application/pdf", "%PDF-1.7"u8.ToArray());
    private static UserProfileResponse Profile(string member = "STUDENT") => new(Guid.NewGuid(), "123", "Camilo", new Guid("a1100000-0000-4000-8000-000000000001"), "ETITC", "Ingeniería", member, "CARD", "ACTIVE", ["USER"]);
    private static VehicleResponse Vehicle(Guid owner, string type = "MOTORCYCLE") => new(Guid.NewGuid(), type, type == "BICYCLE" ? null : "ABC123", type == "BICYCLE" ? "FRAME123" : null,
        "Brand", "Model", "Black", "ACTIVE", owner, "Camilo", "EXPIRED", false, null);
    private static HttpResponseMessage Ok(object value) => new(HttpStatusCode.OK) { Content = JsonContent.Create(value) };
    private static (UserApiService Api, Handler Transport) Service(Func<HttpRequestMessage, Task<HttpResponseMessage>> send)
    { var handler = new Handler(send); return (new UserApiService(new ApiClient(new HttpClient(handler) { BaseAddress = new("https://test.example/") })), handler); }
    private static async Task<AuthSession> Session(string member = "STUDENT") { var session = new AuthSession(new Storage()); await session.SaveAsync("token", Profile(member)); return session; }
    [Theory]
    [InlineData("STUDENT", false)] [InlineData("TEACHER", true)] [InlineData("STAFF", true)]
    public async Task RegisterOptionsRespectMemberType(string member, bool hasCar)
    {
        var session = await Session(member); var service = Service(_ => Task.FromResult(Ok(session.User!)));
        var vm = new RegisterVehicleViewModel(service.Api, session, new Picker(), new Navigation());
        await vm.LoadCommand.ExecuteAsync(null);
        Assert.Equal(hasCar, vm.Types.Any(x => x.Code == "CAR")); Assert.Null(typeof(RegisterVehicleViewModel).GetProperty("Documents")); Assert.Equal("Frente de la Licencia de Tránsito", vm.VerificationLabel);
        vm.SelectedType = vm.Types.Single(x => x.Code == "BICYCLE");
        Assert.Equal("Número de marco", vm.IdentifierLabel); Assert.Equal("Foto de la bicicleta", vm.VerificationLabel);
    }
    [Fact]
    public async Task StudentCannotSubmitInjectedCarSelection()
    {
        var service = Service(_ => Task.FromResult(Ok(new VehicleCreatedResponse(Guid.NewGuid()))));
        var vm = new RegisterVehicleViewModel(service.Api, await Session(), new Picker(), new Navigation()) { SelectedType = new("CAR", "Carro") };
        await vm.SaveCommand.ExecuteAsync(null); Assert.Contains("permitido", vm.ErrorMessage); Assert.Equal(0, service.Transport.Count);
    }
    [Fact]
    public async Task RegisterBicycleMultipartUsesFrameOnlyAndConsecutiveFiles()
    {
        string? body = null; string? mime = null;
        var service = Service(async request => { body = await request.Content!.ReadAsStringAsync(); mime = request.Content.Headers.ContentType!.MediaType; return new(HttpStatusCode.Created) { Content = JsonContent.Create(new VehicleCreatedResponse(Guid.NewGuid())) }; });
        await service.Api.RegisterAsync(new("BICYCLE", "FRAME-01", "Brand", "Model", "Black", Photo));
        Assert.Equal("multipart/form-data", mime); Assert.Contains("FrameNumber", body); Assert.DoesNotContain("name=Plate", body);
        Assert.Contains("VerificationImage.File", body); Assert.DoesNotContain("Photos[", body); Assert.DoesNotContain("Documents[", body); Assert.DoesNotContain("VerificationImage.Type", body);
        Assert.DoesNotContain("OWNERSHIP_SUPPORT", body); Assert.DoesNotContain("OwnerId", body); Assert.Equal(1, service.Transport.Count);
    }
    [Fact]
    public async Task RegisterRequiresVerificationAndRejectsPdfBeforeSending()
    {
        var service = Service(_ => throw new InvalidOperationException()); var vm = new RegisterVehicleViewModel(service.Api, await Session(), new Picker(), new Navigation())
        { Identifier = "ABC123", Brand = "Brand", Model = "Model", Color = "Black" };
        await vm.SaveCommand.ExecuteAsync(null); Assert.Contains("verificación", vm.ErrorMessage); Assert.Equal(0, service.Transport.Count);
        vm.VerificationImage = Pdf; await vm.SaveCommand.ExecuteAsync(null); Assert.NotEmpty(vm.ErrorMessage); Assert.Equal(0, service.Transport.Count);
    }
    [Fact]
    public async Task RegisterSuccessNavigatesToServerReturnedIdWithoutDuplicateTap()
    {
        var created = Guid.NewGuid(); var sent = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously); var reply = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var service = Service(async _ => { sent.SetResult(); await reply.Task; return new(HttpStatusCode.Created) { Content = JsonContent.Create(new VehicleCreatedResponse(created)) }; });
        var nav = new Navigation(); var vm = new RegisterVehicleViewModel(service.Api, await Session(), new Picker(), nav) { Identifier = "ABC123", Brand = "Brand", Model = "Model", Color = "Black", VerificationImage = Photo };

        var saving = vm.SaveCommand.ExecuteAsync(null); await sent.Task; await vm.SaveCommand.ExecuteAsync(null); Assert.Equal(1, service.Transport.Count);
        reply.SetResult(); await saving; Assert.Equal("vehicle-detail", nav.Route); Assert.Equal(created, nav.Arguments!["vehicleId"]); Assert.False(vm.IsBusy);
    }
    [Fact]
    public async Task RenewalReusesExistingEvidenceAndCallsOnlyRenewEndpoint()
    {
        var session = await Session(); var vehicle = Vehicle(session.User!.Id); var detail = new VehicleDetailResponse(vehicle, [],
            [new(Guid.NewGuid(), "VEHICLE_REGISTRATION", null, "reg.pdf", "application/pdf", 8, null, null, "/content/1"), new(Guid.NewGuid(), "INSURANCE", null, "insurance.pdf", "application/pdf", 8, null, null, "/content/2")], new(Guid.NewGuid(), "TRANSIT_LICENSE_FRONT", "verification.png", "image/png", 8, "/verification/content"));
        string? upload = null; var service = Service(async request =>
        {
            if (request.Method == HttpMethod.Post) { upload = request.RequestUri!.AbsolutePath; return new(HttpStatusCode.Created) { Content = JsonContent.Create(new VehicleRenewedResponse(Guid.NewGuid())) }; }
            return request.RequestUri!.AbsolutePath.Contains("academic-periods") ? Ok(new AcademicPeriodResponse(Guid.NewGuid(), "2026-2", new(2026,7,1), new(2026,12,31), "ACTIVE")) : await Task.FromResult(Ok(detail));
        });
        var vm = new RenewRegistrationViewModel(service.Api, new Navigation()) { VehicleId = vehicle.Id };
        await vm.LoadCommand.ExecuteAsync(null); Assert.All(vm.Documents, x => Assert.True(x.HasExisting)); await vm.SaveCommand.ExecuteAsync(null);
        Assert.Equal($"/api/v1/vehicles/{vehicle.Id}/renew", upload); Assert.Empty(vm.ErrorMessage);
    }
    [Fact]
    public async Task RenewalWithoutReplacementsUsesDocumentedEmptyBody()
    {
        long? length = null; string? mediaType = null;
        var service = Service(request => { length = request.Content!.Headers.ContentLength; mediaType = request.Content.Headers.ContentType!.MediaType; return Task.FromResult(new HttpResponseMessage(HttpStatusCode.Created) { Content = JsonContent.Create(new VehicleRenewedResponse(Guid.NewGuid())) }); });
        Assert.True((await service.Api.RenewAsync(Guid.NewGuid(), [])).IsSuccess);
        Assert.Equal(0, length); Assert.Equal("multipart/form-data", mediaType); Assert.Equal(1, service.Transport.Count);
    }
    [Fact]
    public async Task EditVehicleSendsOnlyBrandModelAndColor()
    {
        string? body = null; var service = Service(async request => { body = await request.Content!.ReadAsStringAsync(); return new(HttpStatusCode.NoContent); });
        var vm = new EditVehicleViewModel(service.Api, new Navigation()) { VehicleId = Guid.NewGuid(), Brand = "New", Model = "Model", Color = "Red", Identifier = "READONLY" };
        await vm.SaveCommand.ExecuteAsync(null); using var json = JsonDocument.Parse(body!);
        Assert.Equal(new[] { "brand", "model", "color" }, json.RootElement.EnumerateObject().Select(x => x.Name)); Assert.DoesNotContain("READONLY", body);
    }
    [Fact]
    public async Task ProfileUpdateSendsOnlyAllowedFields()
    {
        string? body = null; var service = Service(async request => { body = await request.Content!.ReadAsStringAsync(); return new(HttpStatusCode.NoContent); });
        var vm = new EditProfileViewModel(service.Api, new Navigation()) { FullName = "Camilo actualizado", Career = "Ingeniería" }; await vm.SaveCommand.ExecuteAsync(null);
        using var json = JsonDocument.Parse(body!); Assert.Equal(new[] { "fullName", "career" }, json.RootElement.EnumerateObject().Select(x => x.Name));
    }
    [Fact]
    public async Task PasswordConfirmationIsLocalAndAllPasswordFieldsAreCleared()
    {
        string? body = null; var service = Service(async request => { body = await request.Content!.ReadAsStringAsync(); return new(HttpStatusCode.NoContent); });
        var vm = new ChangePasswordViewModel(service.Api, new Navigation()) { CurrentPassword = "OldPassword1", NewPassword = "NewPassword2", Confirmation = "wrong" };
        await vm.SaveCommand.ExecuteAsync(null); Assert.Equal(0, service.Transport.Count); Assert.Empty(vm.CurrentPassword); Assert.Empty(vm.NewPassword); Assert.Empty(vm.Confirmation);
        vm.CurrentPassword = "OldPassword1"; vm.NewPassword = vm.Confirmation = "NewPassword2"; await vm.SaveCommand.ExecuteAsync(null);
        using var json = JsonDocument.Parse(body!); Assert.False(json.RootElement.TryGetProperty("confirmation", out _)); Assert.Equal(1, service.Transport.Count); Assert.Empty(vm.NewPassword);
    }
    [Theory]
    [InlineData("bad.pdf", "application/pdf", true)] [InlineData("bad.exe", "application/octet-stream", false)]
    [InlineData("bad.png", "image/png", false)]
    public void FileValidationRejectsUnsupportedTypeOrInvalidContent(string name, string mime, bool photo) =>
        Assert.Throws<UserInputException>(() => AttachmentValidation.Validate(name, mime, "not a valid signature"u8.ToArray(), photo));
    [Fact]
    public void FileValidationEnforcesSizesAndSanitizesNames()
    {
        Assert.Throws<UserInputException>(() => AttachmentValidation.Validate("large.png", "image/png", new byte[5*1024*1024+1], true));
        Assert.Throws<UserInputException>(() => AttachmentValidation.Validate("large.pdf", "application/pdf", new byte[10*1024*1024+1], false));
        Assert.Equal("photo.png", AttachmentValidation.Validate("../../photo.png", "image/png", Photo.Bytes, true).FileName);
    }
    [Fact]
    public void OptionalDocumentDatesAreSentOnlyWhenEnabled()
    {
        var doc = new DocumentInputViewModel("INSURANCE", new Picker()) { File = Pdf };
        Assert.Null(doc.Build()!.IssuedOn); doc.HasIssuedOn = doc.HasExpiresOn = true; doc.IssuedOn = new(2026,10,7); doc.ExpiresOn = new(2026,10,6);
        Assert.Throws<UserInputException>(() => doc.Build());
    }
    [Fact]
    public async Task HistoryUsesPersonalEndpointFiltersAndServerPagination()
    {
        string? url = null; var service = Service(request => { url = request.RequestUri!.PathAndQuery; return Task.FromResult(Ok(new PagedResponse<UniversityParking.Contracts.Parking.ParkingMovementResponse>([], 2, 20, 21, 2))); });
        var vm = new MyHistoryViewModel(service.Api) { DateFrom = new(2026,10,1), DateTo = new(2026,10,7), SelectedVehicle = new(Guid.NewGuid(), "Vehicle"), Page = 2 };
        await vm.NextCommand.ExecuteAsync(null); // No request before page totals are known.
        Assert.Equal(0, service.Transport.Count);
        await vm.RefreshCommand.ExecuteAsync(null); Assert.Contains("/parking/history/me", url); Assert.Contains("dateFrom=2026-10-01", url); Assert.Contains("vehicleId=", url); Assert.DoesNotContain("userId=", url); Assert.Equal(2, vm.Page);
    }
    [Fact]
    public async Task ReversedHistoryRangeMakesNoRequest()
    {
        var service = Service(_ => throw new InvalidOperationException()); var vm = new MyHistoryViewModel(service.Api) { DateFrom = new(2026,10,7), DateTo = new(2026,10,1) };
        await vm.RefreshCommand.ExecuteAsync(null); Assert.Equal(0, service.Transport.Count); Assert.Contains("inicial", vm.ErrorMessage);
    }
    [Fact]
    public async Task NewsUsesPublishedEndpointAndPassesFullItemToDetail()
    {
        var item = new NewsResponse(Guid.NewGuid(), "Aviso", new string('x', 200), "PUBLISHED", DateTimeOffset.UtcNow, null, Guid.NewGuid(), DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);
        string? url = null; var service = Service(request => { url = request.RequestUri!.AbsolutePath; return Task.FromResult(Ok(new PagedResponse<NewsResponse>([item], 1, 20, 1, 1))); });
        var nav = new Navigation(); var vm = new NewsViewModel(service.Api, nav); await vm.RefreshCommand.ExecuteAsync(null);
        Assert.Equal("/api/v1/news", url); Assert.Equal(161, Assert.Single(vm.Items).Preview.Length); await vm.OpenCommand.ExecuteAsync(vm.Items[0]); Assert.Equal("news-detail", nav.Route); Assert.IsType<NewsResponse>(nav.Arguments!["news"]);
    }
    [Fact]
    public async Task EmptyVehicleListHasUsefulEmptyStateAndNetworkFailureHasError()
    {
        var service = Service(_ => Task.FromResult(Ok(Array.Empty<VehicleResponse>()))); var vm = new MyVehiclesViewModel(service.Api, new Navigation());
        await vm.LoadCommand.ExecuteAsync(null); Assert.True(vm.IsEmpty);
        var failed = Service(_ => throw new HttpRequestException()); var failureVm = new MyVehiclesViewModel(failed.Api, new Navigation());
        await failureVm.LoadCommand.ExecuteAsync(null); Assert.False(failureVm.IsEmpty); Assert.Contains("conectar", failureVm.ErrorMessage); Assert.False(failureVm.IsBusy);
    }
    [Fact]
    public async Task PhotoPreviewDownloadsThroughPrivateApiPath()
    {
        var user = Profile(); var vehicle = Vehicle(user.Id) with { VerificationImagePreviewUrl = "/api/v1/vehicles/private/photos/id/content" };
        string? path = null; var service = Service(request => { path = request.RequestUri!.AbsolutePath; return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(Photo.Bytes) }); });
        var card = new VehicleCardViewModel(vehicle, service.Api); await card.LoadPhotoCommand.ExecuteAsync(null); Assert.Equal(Photo.Bytes, card.Photo); Assert.EndsWith("/content", path);
    }
    [Fact]
    public async Task StatusUsesPatchAndCancelledConfirmationDoesNotMutate()
    {
        var session = await Session(); var vehicle = Vehicle(session.User!.Id); var detail = new VehicleDetailResponse(vehicle, [], []); var methods = new List<HttpMethod>();
        var service = Service(request => { methods.Add(request.Method); return Task.FromResult(request.Method == HttpMethod.Patch ? new HttpResponseMessage(HttpStatusCode.NoContent) : request.RequestUri!.AbsolutePath.Contains("academic-periods") ? Ok(new AcademicPeriodResponse(Guid.NewGuid(), "Current", new(2026,1,1), new(2026,12,31), "ACTIVE")) : Ok(detail)); });
        var nav = new Navigation { Confirm = false }; var vm = new VehicleDetailViewModel(service.Api, nav, new Viewer(), session) { VehicleId = vehicle.Id };
        await vm.LoadCommand.ExecuteAsync(null); await vm.ChangeStatusCommand.ExecuteAsync(null); Assert.DoesNotContain(HttpMethod.Patch, methods);
        nav.Confirm = true; await vm.ChangeStatusCommand.ExecuteAsync(null); Assert.Contains(HttpMethod.Patch, methods);
    }
    [Fact]
    public void DatesAndOpenMovementUseBogotaAndNoFakeExit()
    {
        Assert.Equal("06/10/2026 23:59", MobileDates.Display(DateTimeOffset.Parse("2026-10-07T04:59:00Z")));
        var item = new UniversityParking.Contracts.Parking.ParkingMovementResponse(Guid.NewGuid(), Guid.NewGuid(), "User", Guid.NewGuid(), "BICYCLE", "FRAME", Guid.NewGuid(), "Lot", Guid.NewGuid(), "Zone", DateTimeOffset.UtcNow, Guid.NewGuid(), null, null, "OPEN", TimeSpan.FromHours(1));
        var card = new MovementCard(item); Assert.Equal("DENTRO", card.Status); Assert.Equal("Salida: —", card.Exit);
    }
    private sealed class Storage : ISecretStorage
    { public Task<string?> GetAsync(string key) => Task.FromResult<string?>(null); public Task SetAsync(string key, string value) => Task.CompletedTask; public void Remove(string key) { } }
    private sealed class Picker : IAttachmentPicker
    { public Task<PickedAttachment?> PhotoAsync(bool camera) => Task.FromResult<PickedAttachment?>(Photo); public Task<PickedAttachment?> DocumentAsync() => Task.FromResult<PickedAttachment?>(Pdf); }
    private sealed class Viewer : IFileViewer { public Task OpenAsync(string name, string mime, byte[] bytes) => Task.CompletedTask; public void ClearCache() { } }
    private sealed class Navigation : IUserNavigation
    {
        public string? Route; public IReadOnlyDictionary<string, object>? Arguments; public bool Confirm = true;
        public Task GoAsync(string route, IReadOnlyDictionary<string, object>? arguments = null) { Route = route; Arguments = arguments; return Task.CompletedTask; }
        public Task BackAsync() => Task.CompletedTask;
        public Task MessageAsync(string title, string message) => Task.CompletedTask;
        public Task<bool> ConfirmAsync(string title, string message) => Task.FromResult(Confirm);
    }
    private sealed class Handler(Func<HttpRequestMessage, Task<HttpResponseMessage>> send) : HttpMessageHandler
    { public int Count; protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token) { Count++; return send(request); } }
}
