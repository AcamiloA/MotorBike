using Microsoft.EntityFrameworkCore;
using UniversityParking.Application.Common.Abstractions;
using UniversityParking.Application.Common.Pagination;
using UniversityParking.Application.Users;
using UniversityParking.Domain.Users;
using UniversityParking.Domain.Users.ValueObjects;

namespace UniversityParking.Infrastructure.Persistence.Repositories;

public sealed class UserRepository(AppDbContext context) : IUserRepository
{
    public Task<User?> GetByEmailAsync(string normalizedEmail, CancellationToken token) => context.Users.FirstOrDefaultAsync(x=>x.NormalizedEmail==normalizedEmail,token);
    public async Task<User?> GetByIdForUpdateAsync(Guid id, CancellationToken cancellationToken)
    {
        var user = await context.Users.FromSqlInterpolated($"SELECT * FROM users WHERE id = {id} FOR UPDATE").SingleOrDefaultAsync(cancellationToken);
        if (user is not null) await context.Entry(user).ReloadAsync(cancellationToken);
        return user;
    }
    public Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken) => context.Users.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
    public Task<User?> GetByIdentificationNumberAsync(IdentificationNumber number, CancellationToken cancellationToken) => context.Users.FirstOrDefaultAsync(x => x.IdentificationNumber == number, cancellationToken);
    public Task<User?> GetByCardCodeAsync(CardCode code, CancellationToken cancellationToken) => context.Users.FirstOrDefaultAsync(x => x.CardCode == code, cancellationToken);
    public Task<bool> ExistsByIdentificationNumberAsync(IdentificationNumber number, CancellationToken cancellationToken) => context.Users.AnyAsync(x => x.IdentificationNumber == number, cancellationToken);
    public Task<bool> ExistsByCardCodeAsync(CardCode code, CancellationToken cancellationToken) => context.Users.AnyAsync(x => x.CardCode == code, cancellationToken);
    public async Task AddAsync(User user, CancellationToken cancellationToken) => await context.Users.AddAsync(user, cancellationToken);

    public async Task<PagedResult<UserProfile>> SearchAsync(GetUsersQuery query, CancellationToken cancellationToken)
    {
        var selection = context.Users.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var escaped = query.Search.Trim().Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_");
            var pattern = "%" + escaped + "%";
            // Search the converted identifier columns in SQL without exposing IQueryable to Application.
            selection = context.Users.FromSqlInterpolated($"SELECT * FROM users WHERE full_name ILIKE {pattern} ESCAPE '\\' OR identification_number ILIKE {pattern} ESCAPE '\\' OR card_code ILIKE {pattern} ESCAPE '\\' OR normalized_email ILIKE {pattern} ESCAPE '\\'").AsNoTracking();
        }
        if (query.MemberType.HasValue) selection = selection.Where(x => x.MemberType == query.MemberType.Value);
        if (query.UserType.HasValue) selection=selection.Where(x=>x.UserType==query.UserType.Value);
        if (query.Status.HasValue) selection = selection.Where(x => x.Status == query.Status.Value);
        if (query.Role is not null)
            selection = selection.Where(user => (from assignment in context.UserRoles
                join role in context.Roles on assignment.RoleId equals role.Id
                where assignment.UserId == user.Id && role.Code == query.Role
                select assignment).Any());
        var total = await selection.LongCountAsync(cancellationToken);
        var offset = ((long)query.Page - 1) * query.PageSize;
        if (offset >= total) return new PagedResult<UserProfile>([], query.Page, query.PageSize, total);
        var users = await selection.OrderBy(x => x.FullName).ThenBy(x => x.Id)
            .Skip((int)offset).Take(query.PageSize).ToListAsync(cancellationToken);
        var ids = users.Select(x => x.Id).ToArray();
        var universityIds = users.Select(x => x.UniversityId).Distinct().ToArray();
        var universityNames = await context.Universities.AsNoTracking().Where(x => universityIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, x => x.Name, cancellationToken);
        var assignments = await (from assignment in context.UserRoles.AsNoTracking()
            join role in context.Roles.AsNoTracking() on assignment.RoleId equals role.Id
            where ids.Contains(assignment.UserId)
            orderby role.Code
            select new { assignment.UserId, role.Code }).ToListAsync(cancellationToken);
        var lookup = assignments.ToLookup(x => x.UserId, x => x.Code);
        return new PagedResult<UserProfile>(users.Select(x => UserProfile.From(x, lookup[x.Id].ToArray(), universityNames[x.UniversityId])), query.Page, query.PageSize, total);
    }
}
