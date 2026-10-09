using Microsoft.EntityFrameworkCore;
using UniversityParking.Application.Common.Abstractions;
using UniversityParking.Domain.Users;

namespace UniversityParking.Infrastructure.Persistence.Repositories;

public sealed class UserCredentialRepository(AppDbContext context) : IUserCredentialRepository
{
    public async Task AddAsync(UserCredential credential, CancellationToken cancellationToken) =>
        await context.UserCredentials.AddAsync(credential, cancellationToken);
    public async Task<UserCredential?> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken)
    { var item=await context.UserCredentials.FirstOrDefaultAsync(x=>x.UserId==userId,cancellationToken);if(item is not null)await context.Entry(item).ReloadAsync(cancellationToken);return item; }
}
