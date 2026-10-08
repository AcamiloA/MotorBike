using Microsoft.EntityFrameworkCore;
using UniversityParking.Application.Common.Abstractions;
using UniversityParking.Domain.Users;

namespace UniversityParking.Infrastructure.Persistence.Repositories;

public sealed class RoleRepository(AppDbContext context) : IRoleRepository
{
    public Task<Role?> GetByCodeAsync(string code, CancellationToken cancellationToken) =>
        context.Roles.FirstOrDefaultAsync(x => x.Code == code, cancellationToken);
    public async Task AssignAsync(UserRole assignment, CancellationToken cancellationToken) =>
        await context.UserRoles.AddAsync(assignment, cancellationToken);
    public async Task RemoveAsync(Guid userId, Guid roleId, CancellationToken cancellationToken)
    {
        var assignment = await context.UserRoles.SingleOrDefaultAsync(x => x.UserId == userId && x.RoleId == roleId, cancellationToken);
        if (assignment is not null) context.UserRoles.Remove(assignment);
    }
    public async Task<IReadOnlyCollection<string>> GetCodesByUserIdAsync(Guid userId, CancellationToken cancellationToken) =>
        await (from assignment in context.UserRoles.AsNoTracking()
               join role in context.Roles.AsNoTracking() on assignment.RoleId equals role.Id
               where assignment.UserId == userId
               orderby role.Code
               select role.Code).ToListAsync(cancellationToken);
}
