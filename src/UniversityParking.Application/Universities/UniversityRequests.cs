using UniversityParking.Application.Common.Messaging;

namespace UniversityParking.Application.Universities;

public sealed record GetActiveUniversitiesQuery : IQuery<IReadOnlyList<UniversityView>>;
public sealed record UniversityView(Guid Id, string Code, string Name);
