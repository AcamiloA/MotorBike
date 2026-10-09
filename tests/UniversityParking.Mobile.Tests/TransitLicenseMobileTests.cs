using System.Net;
using System.Net.Http.Json;
using UniversityParking.Contracts.Common;
using UniversityParking.Contracts.Vehicles;
using UniversityParking.Mobile.ViewModels;

namespace UniversityParking.Mobile.Tests;

public sealed partial class UserFeatureTests
{
    [Theory]
    [InlineData("TRANSIT_LICENSE_REVIEW_REQUIRED", "No pudimos validar completamente el formato. Intenta tomar una fotografía más clara.", 400)]
    [InlineData("TRANSIT_LICENSE_INVALID_FORMAT", "La imagen no corresponde al formato esperado de una Licencia de Tránsito colombiana.", 400)]
    [InlineData("TRANSIT_LICENSE_UNREADABLE", "No pudimos leer el documento. Evita reflejos, acerca la tarjeta y toma nuevamente la fotografía.", 400)]
    [InlineData("DOCUMENT_OCR_UNAVAILABLE", "No fue posible validar el documento en este momento. Intenta nuevamente.", 503)]
    public async Task OcrErrorsPreserveFieldsAndOnlyDocumentErrorsClearImage(string code, string message, int status)
    {
        var pending = new TaskCompletionSource<HttpResponseMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
        var sent = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var service = Service(_ => { sent.TrySetResult(); return pending.Task; });
        var vm = new RegisterVehicleViewModel(service.Api, await Session(), new Picker(), new Navigation())
        { Identifier = "ABC123", Brand = "Marca", Model = "Modelo", Color = "Color", VerificationImage = Photo };
        var save = vm.SaveCommand.ExecuteAsync(null); await sent.Task;
        Assert.Equal("Guardando evidencia...", vm.ProcessingMessage); Assert.True(vm.IsBusy);
        pending.SetResult(new((HttpStatusCode)status) { Content = JsonContent.Create(new ApiProblemDetails { Status = status, Title = "Error", Detail = message, Code = code, TraceId = "trace" }) });
        await save; Assert.Equal(message, vm.ErrorMessage); Assert.Equal("ABC123", vm.Identifier); Assert.Equal("Marca", vm.Brand);
        Assert.Equal("Modelo", vm.Model); Assert.Equal("Color", vm.Color); Assert.Equal("", vm.ProcessingMessage);
        if (status == 503) Assert.NotNull(vm.VerificationImage); else Assert.Null(vm.VerificationImage);
    }
    [Fact]
    public async Task ReplacementOcrErrorsPreserveImageOnlyOnTechnicalFailure()
    {
        var session = await Session(); var vehicle = Vehicle(session.User!.Id);
        foreach (var code in new[] { "TRANSIT_LICENSE_REVIEW_REQUIRED", "TRANSIT_LICENSE_INVALID_FORMAT", "TRANSIT_LICENSE_UNREADABLE", "DOCUMENT_OCR_UNAVAILABLE" })
        {
            var status = code == "DOCUMENT_OCR_UNAVAILABLE" ? 503 : 400; var writes = 0;
            var service = Service(request =>
            {
                if (request.Method == HttpMethod.Get) return Task.FromResult(Ok(new VehicleDetailResponse(vehicle, [], [])));
                writes++; return Task.FromResult(new HttpResponseMessage((HttpStatusCode)status) { Content = JsonContent.Create(new ApiProblemDetails
                { Status = status, Code = code, Detail = "document-error", TraceId = "trace" }) });
            });
            var vm = new VerificationImageViewModel(service.Api, new Picker(), session, new Navigation()) { VehicleId = vehicle.Id };
            await vm.LoadCommand.ExecuteAsync(null); vm.VerificationImage = Photo; await vm.SaveCommand.ExecuteAsync(null);
            Assert.Equal(1, writes); Assert.False(vm.WriteUncertain); Assert.Equal("", vm.ProcessingMessage);
            if (status == 503) Assert.NotNull(vm.VerificationImage); else Assert.Null(vm.VerificationImage);
        }
    }
    [Fact]
    public async Task BicycleProcessingNeverSaysLicenseValidation()
    {
        var pending = new TaskCompletionSource<HttpResponseMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
        var service = Service(_ => pending.Task);
        var vm = new RegisterVehicleViewModel(service.Api, await Session(), new Picker(), new Navigation());
        vm.SelectedType = vm.Types.Single(x => x.Code == "BICYCLE"); vm.Identifier = "FRAME01"; vm.Brand = "Marca"; vm.Model = "Modelo"; vm.Color = "Color"; vm.VerificationImage = Photo;
        var save = vm.SaveCommand.ExecuteAsync(null); Assert.DoesNotContain("Licencia", vm.ProcessingMessage);
        pending.SetResult(new(HttpStatusCode.Created) { Content = JsonContent.Create(new VehicleCreatedResponse(Guid.NewGuid())) }); await save;
    }
}
