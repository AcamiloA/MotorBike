using FluentValidation;

namespace UniversityParking.Application.Common.Pagination;

public sealed record PageRequest(int Page = 1, int PageSize = 20)
{
    public const int MaximumPageSize = 100;
    public long Offset => ((long)Page - 1) * PageSize;
}

public sealed class PageRequestValidator : AbstractValidator<PageRequest>
{
    public PageRequestValidator()
    {
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1).WithMessage("La página debe ser mayor o igual a 1.");
        RuleFor(x => x.PageSize).InclusiveBetween(1, PageRequest.MaximumPageSize)
            .WithMessage("El tamaño de página debe estar entre 1 y 100.");
    }
}
