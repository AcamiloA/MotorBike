using FluentValidation;

namespace UniversityParking.Application.News;

public sealed class CreateNewsCommandValidator : AbstractValidator<CreateNewsCommand>
{
    public CreateNewsCommandValidator()
    {
        RuleFor(x => x.Title).NotEmpty().MaximumLength(200).WithMessage("El título es obligatorio y admite hasta 200 caracteres.");
        RuleFor(x => x.Content).NotEmpty().WithMessage("El contenido es obligatorio.");
    }
}
public sealed class UpdateNewsCommandValidator : AbstractValidator<UpdateNewsCommand>
{
    public UpdateNewsCommandValidator()
    {
        RuleFor(x => x.NewsId).NotEmpty().WithMessage("La noticia es obligatoria.");
        RuleFor(x => x.Title).NotEmpty().MaximumLength(200).WithMessage("El título es obligatorio y admite hasta 200 caracteres.");
        RuleFor(x => x.Content).NotEmpty().WithMessage("El contenido es obligatorio.");
    }
}
public sealed class PublishNewsCommandValidator : AbstractValidator<PublishNewsCommand>
{ public PublishNewsCommandValidator() => RuleFor(x => x.NewsId).NotEmpty().WithMessage("La noticia es obligatoria."); }
public sealed class ArchiveNewsCommandValidator : AbstractValidator<ArchiveNewsCommand>
{ public ArchiveNewsCommandValidator() => RuleFor(x => x.NewsId).NotEmpty().WithMessage("La noticia es obligatoria."); }
public sealed class GetPublishedNewsQueryValidator : AbstractValidator<GetPublishedNewsQuery>
{
    public GetPublishedNewsQueryValidator()
    {
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1).WithMessage("La página debe ser mayor o igual a 1.");
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100).WithMessage("El tamaño de página debe estar entre 1 y 100.");
    }
}
public sealed class GetAdminNewsQueryValidator : AbstractValidator<GetAdminNewsQuery>
{
    public GetAdminNewsQueryValidator()
    {
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1).WithMessage("La página debe ser mayor o igual a 1.");
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100).WithMessage("El tamaño de página debe estar entre 1 y 100.");
        RuleFor(x => x.Status).IsInEnum().When(x => x.Status.HasValue).WithMessage("El estado no es válido.");
        RuleFor(x => x.Search).MaximumLength(200).WithMessage("La búsqueda admite hasta 200 caracteres.");
    }
}
