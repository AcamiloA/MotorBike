using Microsoft.Extensions.Logging.Abstractions;
using UniversityParking.Application.Files;
using UniversityParking.Application.Vehicles;

namespace UniversityParking.Application.Tests.Vehicles;

public sealed partial class VehicleRegistrationTests
{
    private UpdateVehicleVerificationImageCommandHandler UpdateImage => new(Context, store, store,
        new FileUploadValidator(), store, NullLogger<UploadedFileBatch>.Instance, TestOcr.Service());
    [Fact]
    public async Task LegacyRenewalRequiresExplicitVerificationEvenWithLegacyDocuments()
    {
        var id = (await Handler.Handle(Request(), default)).Value; store.VerificationImages.Clear();
        store.Period!.Close(); store.Period = new("2027-1", new(2027, 1, 1), new(2027, 6, 30), store.UtcNow); store.Period.Activate();
        Assert.Equal("VEHICLE_VERIFICATION_IMAGE_REQUIRED", (await Renewal.Handle(new(id, []), default)).Error!.Code);
        Assert.Single(store.Registrations);
        Assert.True((await UpdateImage.Handle(new(id, Request().VerificationImage), default)).IsSuccess);
        Assert.True((await Renewal.Handle(new(id, []), default)).IsSuccess);
    }
    [Fact]
    public async Task GuardOwnerCannotReplaceVerification()
    {
        var id = (await Handler.Handle(Request(), default)).Value; store.Roles = ["USER", "GUARD"];
        Assert.Equal("FORBIDDEN", (await UpdateImage.Handle(new(id, Request().VerificationImage), default)).Error!.Code);
        Assert.Single(store.Keys);
    }
    [Fact]
    public async Task ReplacementDatabaseFailurePreservesOldFileAndCompensatesNewUpload()
    {
        var id = (await Handler.Handle(Request(), default)).Value; var old = store.Keys.Single(); store.FailSave = true;
        await Assert.ThrowsAsync<IOException>(() => UpdateImage.Handle(new(id, Request().VerificationImage), default));
        Assert.Equal(old, Assert.Single(store.Keys)); Assert.Equal(1, store.Commits);
    }
    [Fact]
    public async Task SuccessfulReplacementKeepsOneImageAndDoesNotTouchLegacyCollections()
    {
        var id = (await Handler.Handle(Request(), default)).Value; var old = store.Keys.Single();
        Assert.True((await UpdateImage.Handle(new(id, Request().VerificationImage), default)).IsSuccess);
        Assert.Single(store.VerificationImages); Assert.Single(store.Keys); Assert.DoesNotContain(old, store.Keys);
        Assert.Empty(store.Photos); Assert.Empty(store.Documents);
        Assert.Single(store.Audits, x => x.Action == "VEHICLE_VERIFICATION_IMAGE_UPDATED");
    }
}
