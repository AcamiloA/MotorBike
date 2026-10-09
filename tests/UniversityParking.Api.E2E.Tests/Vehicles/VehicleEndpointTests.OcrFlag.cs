using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using UniversityParking.Api.E2E.Tests.Auth;
using UniversityParking.Application.Common.Abstractions;
using UniversityParking.Contracts.Vehicles;
using UniversityParking.Domain.Users;

namespace UniversityParking.Api.E2E.Tests.Vehicles;

public sealed partial class VehicleEndpointTests
{
    [Theory] [InlineData("CAR",false,201,0)] [InlineData("MOTORCYCLE",false,201,0)] [InlineData("BICYCLE",false,201,0)]
    [InlineData("BICYCLE",true,201,0)] [InlineData("CAR",true,503,1)] [InlineData("MOTORCYCLE",true,503,1)]
    public async Task FlagControlsRealRegistrationAndEvidenceRemainsRequired(string type,bool enabled,int status,int calls)
    {
        var owner=await CreateUserAsync(MemberType.STAFF);var ocr=new TestDocumentTextExtractor{Fail=true};
        await using var otherFactory=CreateFactory(s=>{s.RemoveAll<IDocumentTextExtractor>();s.AddSingleton<IDocumentTextExtractor>(ocr);},enabled);
        using var other=otherFactory.CreateClient(new(){BaseAddress=new Uri("https://localhost")});await LoginAsync(owner,other);
        using var form=Form(type);var response=await other.PostAsync("/api/v1/vehicles",form);
        Assert.Equal(status,(int)response.StatusCode);Assert.Equal(calls,ocr.Calls);
        await using var db=fixture.CreateContext();
        if(status==201)
        {
            var id=(await response.Content.ReadFromJsonAsync<VehicleCreatedResponse>())!.Id;
            var evidence=await db.VehicleVerificationImages.SingleAsync();Assert.Equal(id,evidence.VehicleId);
            var actual=await other.GetByteArrayAsync($"/api/v1/vehicles/{id}/verification-image/content");Assert.Equal(Png,actual);
        }
        else {Assert.Contains("DOCUMENT_OCR_UNAVAILABLE",await response.Content.ReadAsStringAsync());Assert.Empty(await db.Vehicles.ToArrayAsync());Assert.Empty(await db.VehicleVerificationImages.ToArrayAsync());}
    }
    [Theory] [InlineData("missing")] [InlineData("mime")] [InlineData("size")] [InlineData("magic")]
    public async Task OffDoesNotBypassFileValidation(string condition)
    {
        var owner=await CreateUserAsync();var ocr=new TestDocumentTextExtractor{Fail=true};
        await using var otherFactory=CreateFactory(s=>{s.RemoveAll<IDocumentTextExtractor>();s.AddSingleton<IDocumentTextExtractor>(ocr);},false);
        using var other=otherFactory.CreateClient(new(){BaseAddress=new Uri("https://localhost")});await LoginAsync(owner,other);
        using var form=Form(photo:condition!="missing",photoMime:condition=="mime"?"text/plain":"image/png",photoBytes:condition=="size"?new byte[5242881]:condition=="magic"?[1,2,3]:Png);
        Assert.Equal(HttpStatusCode.BadRequest,(await other.PostAsync("/api/v1/vehicles",form)).StatusCode);Assert.Equal(0,ocr.Calls);
        await using var db=fixture.CreateContext();Assert.Empty(await db.Vehicles.ToArrayAsync());Assert.Empty(await db.VehicleVerificationImages.ToArrayAsync());
    }
    [Theory] [InlineData(false,204,0)] [InlineData(true,503,1)]
    public async Task FlagControlsReplacementWithoutChangingFailureSemantics(bool enabled,int status,int calls)
    {
        var owner=await CreateUserAsync();await LoginAsync(owner);var id=await RegisterAsync();
        await using var db=fixture.CreateContext();var before=await db.VehicleVerificationImages.AsNoTracking().SingleAsync();
        var ocr=new TestDocumentTextExtractor{Fail=true};await using var otherFactory=CreateFactory(s=>{s.RemoveAll<IDocumentTextExtractor>();s.AddSingleton<IDocumentTextExtractor>(ocr);},enabled);
        using var other=otherFactory.CreateClient(new(){BaseAddress=new Uri("https://localhost")});await LoginAsync(owner,other);
        using var form=new MultipartFormDataContent();var bytes=new ByteArrayContent(Png);bytes.Headers.ContentType=new MediaTypeHeaderValue("image/png");form.Add(bytes,"VerificationImage.File","crop.png");
        var response=await other.PutAsync($"/api/v1/vehicles/{id}/verification-image",form);Assert.Equal(status,(int)response.StatusCode);Assert.Equal(calls,ocr.Calls);
        var after=await db.VehicleVerificationImages.AsNoTracking().SingleAsync();Assert.Equal(before.Id,after.Id);Assert.Equal(before.CreatedAt,after.CreatedAt);
        if(enabled){Assert.Equal(before.StorageKey,after.StorageKey);Assert.Contains("DOCUMENT_OCR_UNAVAILABLE",await response.Content.ReadAsStringAsync());}
        else {Assert.NotEqual(before.StorageKey,after.StorageKey);Assert.Equal("crop.png",after.OriginalFileName);Assert.Equal(Png,await other.GetByteArrayAsync($"/api/v1/vehicles/{id}/verification-image/content"));}
    }
}
