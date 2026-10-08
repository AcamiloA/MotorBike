using System.Text.Json.Serialization;

namespace UniversityParking.Contracts.AcademicPeriods;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record CreateAcademicPeriodRequest(string Name, [property: JsonRequired] DateOnly StartsOn, [property: JsonRequired] DateOnly EndsOn);
public sealed record AcademicPeriodCreatedResponse(Guid Id);
public sealed record AcademicPeriodResponse(Guid Id, string Name, DateOnly StartsOn, DateOnly EndsOn, string Status);
