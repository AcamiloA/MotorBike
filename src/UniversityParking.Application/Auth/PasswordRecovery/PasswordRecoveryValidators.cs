using FluentValidation;
using UniversityParking.Application.Common.Validation;
namespace UniversityParking.Application.Auth.PasswordRecovery;
public sealed class RequestPasswordRecoveryValidator : AbstractValidator<RequestPasswordRecoveryCommand>
{ public RequestPasswordRecoveryValidator(){RuleFor(x=>x.Email).NotEmpty().MaximumLength(254).EmailAddress();} }
public sealed class CompletePasswordRecoveryValidator : AbstractValidator<CompletePasswordRecoveryCommand>
{ public CompletePasswordRecoveryValidator(){RuleFor(x=>x.Email).NotEmpty().MaximumLength(254).EmailAddress();RuleFor(x=>x.Code).Matches("^[0-9]{8}$");RuleFor(x=>x.NewPassword).ExistingPasswordPolicy();} }
public sealed class CompleteTemporaryPasswordValidator : AbstractValidator<CompleteTemporaryPasswordCommand>
{ public CompleteTemporaryPasswordValidator(){RuleFor(x=>x.ChallengeId).NotEmpty();RuleFor(x=>x.Token).NotEmpty().MaximumLength(64);RuleFor(x=>x.NewPassword).ExistingPasswordPolicy();} }
public sealed class ResetUserPasswordValidator : AbstractValidator<ResetUserPasswordCommand>
{ public ResetUserPasswordValidator(){RuleFor(x=>x.UserId).NotEmpty();RuleFor(x=>x.TemporaryPassword).ExistingPasswordPolicy();} }
