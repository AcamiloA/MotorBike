using UniversityParking.Application.Parking;
namespace UniversityParking.Application.Tests.Parking;
public sealed class InstitutionalIdentityPayloadTests
{
    [Theory]
    [InlineData("ETITC QR","MTAxNDI4NTU1Mw==","1014285553")]
    [InlineData("UPN QR","1014285553","1014285553")]
    [InlineData("UPN barcode","1014285553","1014285553")]
    [InlineData("Plain whitespace"," \t1014285553\r\n","1014285553")]
    [InlineData("Base64 whitespace"," \tMTAxNDI4NTU1Mw==\r\n","1014285553")]
    [InlineData("Plain priority","MTIz","MTIz")]
    [InlineData("Encoded-looking literal identity","VkVISUNMRUlEPTEx","VkVISUNMRUlEPTEx")]
    public void OneContentResolverWorksRegardlessOfInstitutionOrPhysicalFormat(string scenario,string raw,string expected)
    {var result=new QrIdentityParser().Parse(raw);Assert.True(result.IsSuccess,scenario);Assert.Equal(expected,result.Value.Value);}
    [Theory]
    [InlineData("")] [InlineData(" \r\n")]
    [InlineData("not/base64!")] [InlineData("MTAxNDI4NTU1Mw===")]
    [InlineData("MTAx NDI4NTU1Mw==")] [InlineData("MTAxNDI4NTU1Mx==")]
    [InlineData("/w==")] [InlineData("wK8=")]
    [InlineData("eyJyb2xlIjoiR1VBUkQifQ==")] [InlineData("Role=ADMIN")]
    [InlineData("VehicleId=123")] [InlineData("{\"UserId\":\"123\"}")]
    public void InvalidEncodingOrStructuredClaimsAreControlled(string raw)
    {var result=new QrIdentityParser().Parse(raw);Assert.True(result.IsFailure);Assert.Equal("INVALID_QR_IDENTITY",result.Error!.Code);}
    [Fact] public void ExcessiveRawAndIdentificationLengthsAreRejected()
    {var parser=new QrIdentityParser();Assert.True(parser.Parse(new('1',257)).IsFailure);Assert.True(parser.Parse(new('1',51)).IsFailure);}
}
