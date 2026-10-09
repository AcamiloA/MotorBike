using Microsoft.EntityFrameworkCore;
using UniversityParking.Application.Common.Abstractions;
using UniversityParking.Domain.Users;
namespace UniversityParking.Infrastructure.Persistence.Repositories;
public sealed class PasswordChallengeRepository(AppDbContext db) : IPasswordChallengeRepository
{
    public async Task<PasswordChallenge?> GetAsync(Guid id, CancellationToken token)
    { var item=await db.PasswordChallenges.FirstOrDefaultAsync(x=>x.Id==id,token);if(item is not null)await db.Entry(item).ReloadAsync(token);return item; }
    public async Task<IReadOnlyList<PasswordChallenge>> GetPendingAsync(Guid userId, CancellationToken token) => await db.PasswordChallenges.Where(x=>x.UserId==userId && x.ConsumedAt==null).ToListAsync(token);
    public async Task AddAsync(PasswordChallenge challenge, CancellationToken token) => await db.PasswordChallenges.AddAsync(challenge,token);
}
