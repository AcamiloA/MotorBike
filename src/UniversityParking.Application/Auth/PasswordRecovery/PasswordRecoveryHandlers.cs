using MediatR;
using Microsoft.Extensions.Logging;
using UniversityParking.Application.Common.Abstractions;
using UniversityParking.Application.Common.Results;
using UniversityParking.Application.Common.Errors;
using UniversityParking.Application.Users;
using UniversityParking.Domain.Users;

namespace UniversityParking.Application.Auth.PasswordRecovery;
public sealed class RequestPasswordRecoveryHandler(IUserRepository users,PasswordChallengeService service,IUnitOfWork work,
    IEmailSender emails,ILogger<RequestPasswordRecoveryHandler> logger) : IRequestHandler<RequestPasswordRecoveryCommand,Result>
{
    public async Task<Result> Handle(RequestPasswordRecoveryCommand request,CancellationToken token)
    {
        var email=ContactInformation.Email(request.Email);
        if(!emails.IsEnabled)return Result.Failure(new("EMAIL_DELIVERY_UNAVAILABLE","La recuperación por correo no está disponible en este momento.",ErrorType.Unavailable));
        // Probe the same transport for every syntactically valid address, before looking up any account.
        try{await emails.EnsureAvailableAsync(token);}
        catch(OperationCanceledException) when(token.IsCancellationRequested){throw;}
        catch{logger.LogError("PASSWORD_RECOVERY_DELIVERY_UNAVAILABLE: transporte no disponible.");return Result.Failure(new("EMAIL_DELIVERY_UNAVAILABLE","La recuperación por correo no está disponible en este momento.",ErrorType.Unavailable));}
        await using var transaction=await work.BeginTransactionAsync(token);
        var observed=await users.GetByEmailAsync(email,token);
        if(observed is null) return Result.Success();
        var user=await users.GetByIdForUpdateAsync(observed.Id,token);
        if(user is null || user.Status!=UserStatus.ACTIVE) return Result.Success();
        var created=await service.CreateAsync(user.Id,PasswordChallengePurpose.RECOVERY,token);
        await work.SaveChangesAsync(token);
        try
        {
            await emails.SendAsync(email,"MotorBike Park - Recuperación de contraseña",
                $"Se solicitó recuperar tu contraseña. Código: {created.Token}\nVence en 20 minutos. Si no lo solicitaste, ignora este mensaje.",token);
            await transaction.CommitAsync(token);
        }
        catch(OperationCanceledException) when(token.IsCancellationRequested) { throw; }
        catch { logger.LogError("PASSWORD_RECOVERY_DELIVERY_FAILED: entrega no disponible.");return Result.Failure(new("EMAIL_DELIVERY_UNAVAILABLE","La recuperación por correo no está disponible en este momento.",ErrorType.Unavailable)); }
        // Failed delivery rolls back replacement: previous challenges remain unconsumed.
        return Result.Success();
    }
}
public sealed class CompletePasswordRecoveryHandler(IUserRepository users,IPasswordChallengeRepository challenges,
    IUserCredentialRepository credentials,IPasswordHasher hasher,PasswordChallengeService service,IUnitOfWork work,IClock clock)
    : IRequestHandler<CompletePasswordRecoveryCommand,Result>,IRequestHandler<CompleteTemporaryPasswordCommand,Result>
{
    private static Result Invalid() => Result.Failure(new("PASSWORD_CHALLENGE_INVALID","El código no es válido o ha vencido.",ErrorType.Validation));
    public async Task<Result> Handle(CompletePasswordRecoveryCommand request,CancellationToken token)
    {
        await using var transaction=await work.BeginTransactionAsync(token);
        var observed=await users.GetByEmailAsync(ContactInformation.Email(request.Email),token);
        if(observed is null) return Invalid();
        var user=await users.GetByIdForUpdateAsync(observed.Id,token);
        if(user is null || user.Status!=UserStatus.ACTIVE) return Invalid();
        var challenge=(await challenges.GetPendingAsync(user.Id,token)).Where(x=>x.Purpose==PasswordChallengePurpose.RECOVERY).OrderByDescending(x=>x.CreatedAt).FirstOrDefault();
        return await Complete(user,challenge,request.Code,request.NewPassword,transaction,token);
    }
    public async Task<Result> Handle(CompleteTemporaryPasswordCommand request,CancellationToken token)
    {
        await using var transaction=await work.BeginTransactionAsync(token);
        var observed=await challenges.GetAsync(request.ChallengeId,token);
        if(observed is null) return Invalid();
        var user=await users.GetByIdForUpdateAsync(observed.UserId,token);
        if(user is null || user.Status!=UserStatus.ACTIVE || !user.MustChangePassword) return Invalid();
        var challenge=await challenges.GetAsync(request.ChallengeId,token);
        if(challenge?.Purpose!=PasswordChallengePurpose.TEMPORARY_CHANGE) return Invalid();
        return await Complete(user,challenge,request.Token,request.NewPassword,transaction,token);
    }
    private async Task<Result> Complete(User user,PasswordChallenge? challenge,string secret,string password,IApplicationTransaction transaction,CancellationToken token)
    {
        if(challenge is null) return Invalid();
        if(!challenge.Verify(secret,clock.UtcNow)) { await work.SaveChangesAsync(token);await transaction.CommitAsync(token);return Invalid(); }
        var credential=await credentials.GetByUserIdAsync(user.Id,token);
        if(credential is null) return Invalid();
        if(hasher.Verify(password,credential.PasswordHash)) return Result.Failure(new("PASSWORD_UNCHANGED","Elige una contraseña diferente.",ErrorType.Validation));
        credential.ChangePasswordHash(hasher.Hash(password),clock.UtcNow);user.CompletePasswordChange();
        await service.InvalidateAsync(user.Id,token); await work.SaveChangesAsync(token);await transaction.CommitAsync(token);
        return Result.Success();
    }
}
public sealed class ResetUserPasswordHandler(UserOperationContext operation,IUserRepository users,IUserCredentialRepository credentials,
    IPasswordHasher hasher,PasswordChallengeService service,IUnitOfWork work,IClock clock) : IRequestHandler<ResetUserPasswordCommand,Result>
{
    public async Task<Result> Handle(ResetUserPasswordCommand request,CancellationToken token)
    {
        if(await operation.CheckAccessAsync(true,token) is {} error) return Result.Failure(error);
        await using var transaction=await work.BeginTransactionAsync(token);
        foreach(var id in new[]{operation.ActorId!.Value,request.UserId}.Distinct().Order())await users.GetByIdForUpdateAsync(id,token);
        if(await operation.CheckAccessAsync(true,token) is {} currentError)return Result.Failure(currentError);
        var user=await users.GetByIdForUpdateAsync(request.UserId,token);
        var credential=await credentials.GetByUserIdAsync(request.UserId,token);
        if(user is null || credential is null) return Result.Failure(UserErrors.NotFound);
        credential.ChangePasswordHash(hasher.Hash(request.TemporaryPassword),clock.UtcNow);user.RequirePasswordChange();
        await service.InvalidateAsync(user.Id,token);await operation.AuditAsync("USER_PASSWORD_RESET",user.Id,null,new{MustChangePassword=true},token);
        await work.SaveChangesAsync(token);await transaction.CommitAsync(token);return Result.Success();
    }
}
