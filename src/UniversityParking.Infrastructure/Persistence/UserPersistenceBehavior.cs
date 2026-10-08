using MediatR;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using UniversityParking.Application.Common.Abstractions;
using UniversityParking.Application.Common.Errors;
using UniversityParking.Application.Common.Results;
using UniversityParking.Application.Users;

namespace UniversityParking.Infrastructure.Persistence;

// Serialize changes to a user's status/profile/roles and commit their audit in the same transaction.
// Database constraint translation stays outside Application and Domain.
public sealed class UserPersistenceBehavior<TRequest, TResponse>(AppDbContext context, ICurrentUser actor)
    : IPipelineBehavior<TRequest, TResponse> where TRequest : notnull where TResponse : IResult<TResponse>
{
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        if (request is not IUserWriteCommand) return await next(cancellationToken);
        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            Guid? targetId = request switch
            {
                UpdateMyProfileCommand => actor.UserId,
                UpdateUserCommand command => command.UserId,
                ActivateUserCommand command => command.UserId,
                DeactivateUserCommand command => command.UserId,
                AssignRoleCommand command => command.UserId,
                RemoveRoleCommand command => command.UserId,
                _ => null
            };
            foreach (var id in new[] { actor.UserId, targetId }.Where(x => x.HasValue).Select(x => x!.Value).Distinct().Order())
                await context.Users.FromSqlInterpolated($"SELECT * FROM users WHERE id = {id} FOR UPDATE").LoadAsync(cancellationToken);
            var response = await next(cancellationToken);
            if (response.IsSuccess) await transaction.CommitAsync(cancellationToken);
            return response;
        }
        catch (DbUpdateException exception) when (exception.InnerException is
            PostgresException { SqlState: PostgresErrorCodes.UniqueViolation } postgres &&
            postgres.ConstraintName is "ux_users_identification_number" or "ux_users_card_code")
        {
            var constraint = ((PostgresException)exception.InnerException!).ConstraintName;
            return TResponse.Failure(constraint == "ux_users_identification_number" ? UserErrors.AlreadyExists : UserErrors.CardCodeAlreadyExists);
        }
    }
}
