using UniversityParking.Application.Common.Abstractions;
using UniversityParking.Domain.Users;
using UniversityParking.Domain.Users.ValueObjects;

namespace UniversityParking.Application.Tests.Auth;

internal sealed class FakeClock : IClock
{
    public DateTimeOffset UtcNow { get; set; } = new(2026, 10, 7, 12, 0, 0, TimeSpan.Zero);
}

internal sealed class FakeCurrentUser : ICurrentUser
{
    public Guid? UserId { get; set; }
    public bool IsAuthenticated { get; set; } = true;
    public IReadOnlyCollection<string> Roles { get; set; } = [RoleCodes.User];
    public MemberType? MemberType => UniversityParking.Domain.Users.MemberType.STAFF;
    public bool IsInRole(string roleCode) => IsAuthenticated && Roles.Contains(roleCode, StringComparer.Ordinal);
}

internal sealed class FakeUsers : IUserRepository
{
    public Task<User?> GetByEmailAsync(string email,CancellationToken token)=>Task.FromResult(Values.Values.FirstOrDefault(x=>x.NormalizedEmail==email));
    public Task<User?> GetByIdForUpdateAsync(Guid id, CancellationToken cancellationToken) => GetByIdAsync(id, cancellationToken);
    public Task<UniversityParking.Application.Common.Pagination.PagedResult<UniversityParking.Application.Users.UserProfile>> SearchAsync(
        UniversityParking.Application.Users.GetUsersQuery query, CancellationToken cancellationToken) =>
        Task.FromResult(new UniversityParking.Application.Common.Pagination.PagedResult<UniversityParking.Application.Users.UserProfile>(
            Values.Values.Take(query.PageSize).Select(x => UniversityParking.Application.Users.UserProfile.From(x, [], "ETITC")), query.Page, query.PageSize, Values.Count));
    public Dictionary<Guid, User> Values { get; } = [];
    public Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(Values.GetValueOrDefault(id));
    }
    public Task<User?> GetByIdentificationNumberAsync(IdentificationNumber number, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(Values.Values.FirstOrDefault(x => x.IdentificationNumber == number));
    }
    public Task<User?> GetByCardCodeAsync(CardCode code, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(Values.Values.FirstOrDefault(x => x.CardCode == code));
    }
    public async Task<bool> ExistsByIdentificationNumberAsync(IdentificationNumber number, CancellationToken cancellationToken) =>
        await GetByIdentificationNumberAsync(number, cancellationToken) is not null;
    public async Task<bool> ExistsByCardCodeAsync(CardCode code, CancellationToken cancellationToken) =>
        await GetByCardCodeAsync(code, cancellationToken) is not null;
    public Task AddAsync(User user, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Values.Add(user.Id, user);
        return Task.CompletedTask;
    }
}

internal sealed class FakeCredentials : IUserCredentialRepository
{
    public Task AddAsync(UserCredential credential, CancellationToken cancellationToken)
    {
        Value = credential;
        return Task.CompletedTask;
    }
    public UserCredential? Value { get; set; }
    public Task<UserCredential?> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(Value?.UserId == userId ? Value : null);
    }
}

internal sealed class FakeRoles : IRoleRepository
{
    public Dictionary<string, Role> Values { get; } = new[] { RoleCodes.User, RoleCodes.Guard, RoleCodes.Admin }
        .ToDictionary(x => x, x => new Role(x));
    public List<UserRole> Assignments { get; } = [];
    public Task<Role?> GetByCodeAsync(string code, CancellationToken cancellationToken) => Task.FromResult(Values.GetValueOrDefault(code));
    public Task AssignAsync(UserRole assignment, CancellationToken cancellationToken)
    {
        Assignments.Add(assignment);
        return Task.CompletedTask;
    }
    public Task RemoveAsync(Guid userId, Guid roleId, CancellationToken cancellationToken)
    {
        Assignments.RemoveAll(x => x.UserId == userId && x.RoleId == roleId);
        return Task.CompletedTask;
    }
    public IReadOnlyCollection<string> Codes { get; set; } = [RoleCodes.User];
    public Task<IReadOnlyCollection<string>> GetCodesByUserIdAsync(Guid userId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(Codes);
    }
}

internal sealed class FakePasswordHasher : IPasswordHasher
{
    public string Hash(string password) => "test-hash:" + password;
    public bool Verify(string password, string passwordHash) => passwordHash == Hash(password);
}

internal sealed class FakeTokenService : ITokenService
{
    public TokenUser? IssuedFor { get; private set; }
    public AccessToken CreateAccessToken(TokenUser user)
    {
        IssuedFor = user;
        return new AccessToken("test-only-token", new DateTimeOffset(2026, 10, 7, 20, 0, 0, TimeSpan.Zero));
    }
}

internal sealed class FakeUnitOfWork : IUnitOfWork
{
    public int SaveCount { get; private set; }
    public CancellationToken LastToken { get; private set; }
    public Task<int> SaveChangesAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        LastToken = cancellationToken;
        SaveCount++;
        return Task.FromResult(1);
    }
    public Task<IApplicationTransaction> BeginTransactionAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult<IApplicationTransaction>(new FakeTransaction());
    }
    private sealed class FakeTransaction : IApplicationTransaction
    {
        public bool Committed { get; private set; }
        public bool RolledBack { get; private set; }
        public bool Disposed { get; private set; }
        public Task CommitAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Committed = true;
            return Task.CompletedTask;
        }
        public Task RollbackAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            RolledBack = true;
            return Task.CompletedTask;
        }
        public ValueTask DisposeAsync()
        {
            Disposed = true;
            return ValueTask.CompletedTask;
        }
    }
}
