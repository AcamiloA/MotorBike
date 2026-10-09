using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using UniversityParking.Application.Documents;
using UniversityParking.Application.Files;
using UniversityParking.Application.Vehicles;
using UniversityParking.Domain.Users;
using UniversityParking.Domain.Vehicles;

namespace UniversityParking.Application.Tests.Vehicles;

public sealed partial class VehicleRegistrationTests
{
    private RegisterVehicleCommandHandler FlagHandler(TestOcr ocr,bool enabled)=>new(Context,store,store,store,store,store,store,new FileUploadValidator(),store,
        NullLogger<UploadedFileBatch>.Instance,new TransitLicenseValidationService(ocr,new TransitLicenseFormatValidator(),Options.Create(new DocumentOcrOptions{Enabled=enabled})));
    [Theory] [InlineData(VehicleType.CAR)] [InlineData(VehicleType.MOTORCYCLE)]
    public async Task DisabledOcrStoresRequiredImageWithoutTouchingUnavailableExtractor(VehicleType type)
    {
        store.ChangeMember(MemberType.STAFF);var ocr=new TestOcr{Unavailable=true};var result=await FlagHandler(ocr,false).Handle(Request(type),default);
        Assert.True(result.IsSuccess);Assert.Equal(0,ocr.Calls);Assert.Single(store.Vehicles);Assert.Single(store.VerificationImages);Assert.Single(store.Keys);Assert.Equal(1,store.Commits);
        Assert.Equal(VehicleVerificationImageType.TRANSIT_LICENSE_FRONT,store.VerificationImages.Single().Type);
    }
    [Theory] [InlineData(false)] [InlineData(true)]
    public async Task BicycleNeverDependsOnOcrFlag(bool enabled)
    {
        var ocr=new TestOcr{Unavailable=true};Assert.True((await FlagHandler(ocr,enabled).Handle(Request(VehicleType.BICYCLE),default)).IsSuccess);
        Assert.Equal(0,ocr.Calls);Assert.Equal(VehicleVerificationImageType.BICYCLE_PHOTO,store.VerificationImages.Single().Type);
    }
    [Theory] [InlineData("missing")] [InlineData("mime")] [InlineData("oversize")] [InlineData("magic")]
    public async Task DisabledOcrStillRejectsMissingAndInvalidEvidence(string condition)
    {
        var ocr=new TestOcr{Unavailable=true};var request=Request() with{VerificationImage=condition switch
        {"missing"=>null,"mime"=>Source("image.png","text/plain",FileValidationTests.Png),"oversize"=>Source("large.png","image/png",new byte[5242881]),_=>Source("image.png","image/png",[1,2,3])}};
        Assert.True((await FlagHandler(ocr,false).Handle(request,default)).IsFailure);Assert.Equal(0,ocr.Calls);Assert.Empty(store.Vehicles);Assert.Empty(store.VerificationImages);Assert.Empty(store.Keys);
    }
    [Fact] public async Task EnabledTechnicalFailureHasNoSilentFallback()
    {
        var ocr=new TestOcr{Unavailable=true};var result=await FlagHandler(ocr,true).Handle(Request(),default);
        Assert.Equal("DOCUMENT_OCR_UNAVAILABLE",result.Error!.Code);Assert.Equal(1,ocr.Calls);Assert.Empty(store.Keys);Assert.Empty(store.VerificationImages);
    }
    [Fact] public async Task DisabledOcrAlsoAllowsReplacementWithoutCallingExtractor()
    {
        var id=(await Handler.Handle(Request(),default)).Value;var previous=store.VerificationImages.Single();var oldId=previous.Id;var created=previous.CreatedAt;
        var ocr=new TestOcr{Unavailable=true};var service=new TransitLicenseValidationService(ocr,new TransitLicenseFormatValidator(),Options.Create(new DocumentOcrOptions()));
        var handler=new UpdateVehicleVerificationImageCommandHandler(Context,store,store,new FileUploadValidator(),store,NullLogger<UploadedFileBatch>.Instance,service);
        Assert.True((await handler.Handle(new(id,Source("crop.png","image/png",FileValidationTests.Png)),default)).IsSuccess);
        Assert.Equal(0,ocr.Calls);Assert.Equal(oldId,store.VerificationImages.Single().Id);Assert.Equal(created,store.VerificationImages.Single().CreatedAt);Assert.Equal("crop.png",store.VerificationImages.Single().OriginalFileName);
    }
}
