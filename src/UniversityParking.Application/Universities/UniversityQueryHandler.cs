using MediatR;
using UniversityParking.Application.Common.Abstractions;
using UniversityParking.Application.Common.Results;

namespace UniversityParking.Application.Universities;

public sealed class GetActiveUniversitiesQueryHandler(IUniversityRepository universities)
    : IRequestHandler<GetActiveUniversitiesQuery, Result<IReadOnlyList<UniversityView>>>
{
    public async Task<Result<IReadOnlyList<UniversityView>>> Handle(GetActiveUniversitiesQuery request, CancellationToken cancellationToken)
    {
        var values = await universities.GetActiveAsync(cancellationToken);
        return Result<IReadOnlyList<UniversityView>>.Success(values.Select(x => new UniversityView(x.Id, x.Code, x.Name)).ToArray());
    }
}
