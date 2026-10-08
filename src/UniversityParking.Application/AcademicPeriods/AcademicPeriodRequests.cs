using FluentValidation;
using UniversityParking.Application.Common.Messaging;
using UniversityParking.Domain.AcademicPeriods;

namespace UniversityParking.Application.AcademicPeriods;

public sealed record CreateAcademicPeriodCommand(string Name, DateOnly StartsOn, DateOnly EndsOn) : ICommand<Guid>;
public sealed record ActivateAcademicPeriodCommand(Guid AcademicPeriodId) : ICommand;
public sealed record CloseAcademicPeriodCommand(Guid AcademicPeriodId) : ICommand;
public sealed record GetCurrentAcademicPeriodQuery : IQuery<AcademicPeriodView>;
public sealed record GetAcademicPeriodsQuery : IQuery<IReadOnlyList<AcademicPeriodView>>;
public sealed record AcademicPeriodView(Guid Id, string Name, DateOnly StartsOn, DateOnly EndsOn, AcademicPeriodStatus Status)
{
    public static AcademicPeriodView From(AcademicPeriod period) => new(period.Id, period.Name, period.StartsOn, period.EndsOn, period.Status);
}
public sealed class CreateAcademicPeriodCommandValidator : AbstractValidator<CreateAcademicPeriodCommand>
{
    public CreateAcademicPeriodCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty().WithMessage("El nombre es obligatorio.").MaximumLength(50).WithMessage("El nombre admite hasta 50 caracteres.");
        RuleFor(x => x.StartsOn).LessThan(x => x.EndsOn).WithMessage("El inicio debe ser anterior al fin del periodo.");
    }
}
public sealed class ActivateAcademicPeriodCommandValidator : AbstractValidator<ActivateAcademicPeriodCommand>
{ public ActivateAcademicPeriodCommandValidator() => RuleFor(x => x.AcademicPeriodId).NotEmpty().WithMessage("El identificador es obligatorio."); }
public sealed class CloseAcademicPeriodCommandValidator : AbstractValidator<CloseAcademicPeriodCommand>
{ public CloseAcademicPeriodCommandValidator() => RuleFor(x => x.AcademicPeriodId).NotEmpty().WithMessage("El identificador es obligatorio."); }
