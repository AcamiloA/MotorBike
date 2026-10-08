using UniversityParking.Application.Common.Abstractions;
using UniversityParking.Application.Common.Authorization;
using UniversityParking.Application.Common.Pagination;
using UniversityParking.Application.Incidents;
using UniversityParking.Application.Tests.Auth;
using UniversityParking.Domain.Auditing;
using UniversityParking.Domain.Incidents;
using UniversityParking.Domain.Users;
using UniversityParking.Domain.Users.ValueObjects;

namespace UniversityParking.Application.Tests.Incidents;

public sealed class IncidentUseCaseTests
{
    private readonly FakeClock clock = new();
    private readonly FakeCurrentUser actor = new() { Roles = [RoleCodes.Admin] };
    private readonly FakeUsers users = new();
    private readonly FakeRoles roles = new() { Codes = [RoleCodes.Admin] };
    private readonly FakeUnitOfWork unit = new();
    private readonly Store store = new();
    private readonly AdministrationOperationContext operation;
    public IncidentUseCaseTests()
    {
        var user = new User(new IdentificationNumber("123"), "Admin", UniversityParking.Domain.Universities.UniversityIds.Etitc, null, MemberType.STAFF, new CardCode("CARD"), clock.UtcNow);
        users.Values[user.Id] = user; actor.UserId = user.Id;
        store.Incident = new(Guid.NewGuid(), user.Id, IncidentType.DAMAGE, "Daño", clock.UtcNow, clock.UtcNow);
        operation = new(actor, users, roles, store, clock, new Request());
    }
    [Theory]
    [InlineData("USER")] [InlineData("GUARD")]
    public async Task ResolveAndCancelRequireAdminEvenWhenCalledWithoutHttp(string role)
    {
        actor.Roles = roles.Codes = [role];
        Assert.Equal("FORBIDDEN", (await new ResolveIncidentCommandHandler(operation, actor, store, unit)
            .Handle(new(store.Incident!.Id, "Revisado"), default)).Error!.Code);
        Assert.Equal("FORBIDDEN", (await new CancelIncidentCommandHandler(operation, store, unit)
            .Handle(new(store.Incident.Id), default)).Error!.Code);
        Assert.Equal(0, store.LockCount); Assert.Equal(0, unit.SaveCount);
    }
    [Fact]
    public async Task FreshRolesOverrideStaleTokenClaims()
    {
        roles.Codes = [RoleCodes.User];
        var result = await new ResolveIncidentCommandHandler(operation, actor, store, unit).Handle(new(store.Incident!.Id, "Revisado"), default);
        Assert.Equal("FORBIDDEN", result.Error!.Code); Assert.Empty(store.Audits);
    }
    [Fact]
    public async Task InactiveActorCannotReadIncident()
    {
        users.Values[actor.UserId!.Value].Deactivate(clock.UtcNow);
        var result = await new GetIncidentByIdQueryHandler(operation, store).Handle(new(store.Incident!.Id), default);
        Assert.Equal("USER_INACTIVE", result.Error!.Code);
    }
    [Fact]
    public async Task ResolveUsesLockedAggregateClockActorAndAudit()
    {
        clock.UtcNow = clock.UtcNow.AddHours(1);
        var result = await new ResolveIncidentCommandHandler(operation, actor, store, unit).Handle(new(store.Incident!.Id, "Revisado"), default);
        Assert.True(result.IsSuccess); Assert.Equal(1, store.LockCount); Assert.Equal(1, unit.SaveCount);
        Assert.Equal(actor.UserId, store.Incident.ResolvedBy); Assert.Equal(clock.UtcNow, store.Incident.ResolvedAt);
        var audit = Assert.Single(store.Audits); Assert.Equal("INCIDENT_RESOLVED", audit.Action); Assert.Equal(actor.UserId, audit.ActorUserId);
    }
    [Fact]
    public async Task RepeatCancellationDoesNotSaveAuditOrChangeReason()
    {
        var handler = new CancelIncidentCommandHandler(operation, store, unit);
        Assert.True((await handler.Handle(new(store.Incident!.Id, "Duplicado"), default)).IsSuccess);
        clock.UtcNow = clock.UtcNow.AddHours(1);
        Assert.True((await handler.Handle(new(store.Incident.Id, "Otro"), default)).IsSuccess);
        Assert.Equal(1, unit.SaveCount); Assert.Single(store.Audits); Assert.Equal("Duplicado", store.Incident.Resolution);
    }
    [Theory]
    [InlineData(true)] [InlineData(false)]
    public async Task TerminalStatesCannotResolveAgain(bool resolved)
    {
        if (resolved) store.Incident!.Resolve("Listo", actor.UserId!.Value, clock.UtcNow);
        else store.Incident!.Cancel(clock.UtcNow);
        var result = await new ResolveIncidentCommandHandler(operation, actor, store, unit).Handle(new(store.Incident.Id, "Otro"), default);
        Assert.Equal(resolved ? "INCIDENT_ALREADY_RESOLVED" : "INCIDENT_NOT_OPEN", result.Error!.Code);
        Assert.Equal(0, unit.SaveCount);
    }
    [Fact]
    public async Task MissingIncidentReturnsNotFoundWithoutSaving()
    {
        store.Incident = null;
        Assert.Equal("INCIDENT_NOT_FOUND", (await new CancelIncidentCommandHandler(operation, store, unit).Handle(new(Guid.NewGuid()), default)).Error!.Code);
        Assert.Equal(0, unit.SaveCount);
    }
    [Fact]
    public async Task UnauthorizedAttachmentReadNeverTouchesStorage()
    {
        actor.Roles = roles.Codes = [RoleCodes.User];
        var storage = new Storage();
        var result = await new GetIncidentAttachmentContentQueryHandler(operation, store, storage).Handle(new(store.Incident!.Id, Guid.NewGuid()), default);
        Assert.Equal("FORBIDDEN", result.Error!.Code); Assert.Equal(0, storage.AccessCount);
    }
    [Fact]
    public async Task MismatchedAttachmentNeverSignsOrOpensStorage()
    {
        var storage = new Storage();
        var result = await new GetIncidentAttachmentContentQueryHandler(operation, store, storage).Handle(new(store.Incident!.Id, Guid.NewGuid()), default);
        Assert.Equal("INCIDENT_NOT_FOUND", result.Error!.Code); Assert.Equal(0, storage.AccessCount);
    }
    [Theory]
    [InlineData(true)] [InlineData(false)]
    public async Task AuthorizedAttachmentReturnsPrivateStreamOrSignedUrl(bool signed)
    {
        store.Attachment = new(store.Incident!.Id, "incidents/evidence.pdf", "evidence.pdf", "application/pdf", 8, clock.UtcNow);
        var storage = new Storage { Signed = signed };
        var result = await new GetIncidentAttachmentContentQueryHandler(operation, store, storage).Handle(new(store.Incident.Id, store.Attachment.Id), default);
        Assert.True(result.IsSuccess); Assert.Equal(signed, result.Value.ReadUrl is not null);
        Assert.Equal(signed ? 1 : 2, storage.AccessCount); result.Value.Content?.Dispose();
    }
    [Theory]
    [InlineData(0,20)] [InlineData(1,101)] [InlineData(1,0)]
    public void InvalidPageRejectedBeforeQuery(int page, int size) =>
        Assert.False(new GetIncidentsQueryValidator().Validate(new GetIncidentsQuery(Page: page, PageSize: size)).IsValid);
    private sealed class Request : IRequestContext { public string? IpAddress => "127.0.0.1"; public string? TraceId => "incident-test"; }
    private sealed class Store : IIncidentRepository, IAuditLogRepository
    {
        public Incident? Incident; public IncidentAttachment? Attachment; public int LockCount;
        public List<AuditLog> Audits { get; } = [];
        public Task AddAsync(AuditLog audit, CancellationToken token) { Audits.Add(audit); return Task.CompletedTask; }
        public Task AddAsync(Incident incident, CancellationToken token) { Incident = incident; return Task.CompletedTask; }
        public Task<Incident?> GetByIdAsync(Guid id, CancellationToken token) => Task.FromResult(Incident?.Id == id ? Incident : null);
        public Task<Incident?> GetByIdForUpdateAsync(Guid id, CancellationToken token) { LockCount++; return GetByIdAsync(id, token); }
        public Task AddAttachmentAsync(IncidentAttachment attachment, CancellationToken token) { Attachment = attachment; return Task.CompletedTask; }
        public Task<IReadOnlyList<IncidentAttachment>> GetAttachmentsAsync(Guid id, CancellationToken token) => Task.FromResult<IReadOnlyList<IncidentAttachment>>(Attachment?.IncidentId == id ? [Attachment] : []);
        public Task<IncidentAttachment?> GetAttachmentAsync(Guid incidentId, Guid attachmentId, CancellationToken token) =>
            Task.FromResult(Attachment?.IncidentId == incidentId && Attachment.Id == attachmentId ? Attachment : null);
        public Task<PagedResult<Incident>> SearchAsync(IncidentFilter filter, CancellationToken token) => Task.FromResult(new PagedResult<Incident>([], filter.Page, filter.PageSize, 0));
    }
    private sealed class Storage : IFileStorage
    {
        public int AccessCount; public bool Signed;
        public Task<Uri?> GetReadUrlAsync(string key, CancellationToken token) { AccessCount++; return Task.FromResult<Uri?>(Signed ? new("https://private.example/evidence?signature=test") : null); }
        public Task<Stream> OpenReadAsync(string key, CancellationToken token) { AccessCount++; return Task.FromResult<Stream>(new MemoryStream()); }
        public Task DeleteAsync(string key, CancellationToken token) => Task.CompletedTask;
        public Task<StoredFile> UploadAsync(FileUpload upload, CancellationToken token) => Task.FromResult(new StoredFile(upload.StorageKey, upload.ContentType, upload.SizeBytes));
    }
}
