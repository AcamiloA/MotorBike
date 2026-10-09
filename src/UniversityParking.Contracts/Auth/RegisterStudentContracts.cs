using System.Text.Json.Serialization;

namespace UniversityParking.Contracts.Auth;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record RegisterStudentRequest(string IdentificationNumber, string FullName,
    [property: JsonRequired] Guid UniversityId, string Career, string CardCode, string Password);

public sealed record RegisterStudentResponse(Guid UserId, string Status);
