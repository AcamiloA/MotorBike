using System.Text.Json;
using UniversityParking.Contracts.Auth;

namespace UniversityParking.Api.E2E.Tests.Auth;

public sealed class RegisterStudentContractTests
{
    private static readonly JsonSerializerOptions Json=new(JsonSerializerDefaults.Web);
    private static RegisterStudentRequest Request()=>new("001","Nombre",Guid.NewGuid(),"Carrera","CARD","Password1");
    [Theory][InlineData("memberType")][InlineData("roles")][InlineData("status")][InlineData("autoApprove")][InlineData("confirmPassword")][InlineData("isAdmin")]
    public void PayloadRejectsClientControlledPrivilegesAndConfirmation(string field)
    {
        var json=JsonSerializer.Serialize(Request(),Json);json=json[..^1]+",\""+field+"\":null}";
        Assert.Throws<JsonException>(()=>JsonSerializer.Deserialize<RegisterStudentRequest>(json,Json));
    }
    [Fact] public void MissingUniversityReferenceIsRejected()
    {Assert.Throws<JsonException>(()=>JsonSerializer.Deserialize<RegisterStudentRequest>("{\"identificationNumber\":\"001\",\"fullName\":\"Nombre\",\"career\":\"Carrera\",\"cardCode\":\"CARD\",\"password\":\"Password1\"}",Json));}
    [Fact] public void RequestAndResponseHaveOnlyTheirDeclaredPublicFields()
    {
        using var request=JsonDocument.Parse(JsonSerializer.Serialize(Request(),Json));
        Assert.Equal(new[]{"cardCode","career","fullName","identificationNumber","password","universityId"},request.RootElement.EnumerateObject().Select(x=>x.Name).Order());
        using var response=JsonDocument.Parse(JsonSerializer.Serialize(new RegisterStudentResponse(Guid.NewGuid(),"PENDING"),Json));
        Assert.Equal(new[]{"status","userId"},response.RootElement.EnumerateObject().Select(x=>x.Name).Order());
    }
}
