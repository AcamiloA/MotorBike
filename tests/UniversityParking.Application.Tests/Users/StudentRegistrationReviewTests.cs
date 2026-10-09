using UniversityParking.Application.Common.Abstractions;
using UniversityParking.Application.Tests.Auth;
using UniversityParking.Application.Users;
using UniversityParking.Domain.Auditing;
using UniversityParking.Domain.Universities;
using UniversityParking.Domain.Users;

namespace UniversityParking.Application.Tests.Users;

public sealed class StudentRegistrationReviewTests
{
    private readonly FakeUsers users=new();private readonly FakeRoles roles=new(){Codes=["USER","ADMIN"]};
    private readonly FakeCurrentUser actor=new(){Roles=["USER","ADMIN"]};private readonly FakeUnitOfWork work=new();
    private readonly FakeClock clock=new();private readonly Audit audit=new();private readonly User target;
    private StudentRegistrationReview Review=>new(new UserOperationContext(actor,users,roles,audit,clock,new RequestContext()),users,new FakeUniversities(),work);
    public StudentRegistrationReviewTests()
    {
        var admin=new User(new("ADMIN"),"Admin",UniversityIds.Etitc,null,MemberType.STAFF,new("ADMIN"),clock.UtcNow);
        users.Values.Add(admin.Id,admin);actor.UserId=admin.Id;target=User.CreateStudentRegistration(new("001"),"Nombre",UniversityIds.Etitc,"Carrera",new("CARD"),false,clock.UtcNow);users.Values.Add(target.Id,target);
    }
    [Theory][InlineData(true)][InlineData(false)]
    public async Task ReviewPreservesProfileAndProducesTrustedAudit(bool approve)
    {
        Assert.True((await Review.ReviewAsync(target.Id,approve,default)).IsSuccess);Assert.Equal(approve?UserStatus.ACTIVE:UserStatus.REJECTED,target.Status);
        Assert.Equal("Nombre",target.FullName);Assert.Equal(UniversityIds.Etitc,target.UniversityId);Assert.Equal(1,work.SaveCount);
        Assert.Equal(actor.UserId,Assert.Single(audit.Values).ActorUserId);Assert.Contains("PENDING",audit.Values[0].OldValues!);Assert.Contains(approve?"ACTIVE":"REJECTED",audit.Values[0].NewValues!);
    }
    [Theory][InlineData(true)][InlineData(false)]
    public async Task PersistedAdminRoleIsRechecked(bool approve)
    {roles.Codes=["USER"];Assert.Equal("FORBIDDEN",(await Review.ReviewAsync(target.Id,approve,default)).Error!.Code);Assert.Equal(UserStatus.PENDING,target.Status);Assert.Empty(audit.Values);Assert.Equal(0,work.SaveCount);}
    [Theory][InlineData(true)][InlineData(false)]
    public async Task PendingNonStudentCannotBeReviewed(bool approve)
    {target.Update(target.FullName,target.UniversityId,null,MemberType.STAFF,target.CardCode,clock.UtcNow);Assert.Equal("INVALID_USER_STATUS_TRANSITION",(await Review.ReviewAsync(target.Id,approve,default)).Error!.Code);Assert.Empty(audit.Values);Assert.Equal(0,work.SaveCount);}
    [Theory][InlineData(true)][InlineData(false)]
    public async Task MissingUserFailsWithoutSaving(bool approve)
    {Assert.Equal("USER_NOT_FOUND",(await Review.ReviewAsync(Guid.NewGuid(),approve,default)).Error!.Code);Assert.Equal(0,work.SaveCount);}
    [Fact] public void CommandsRequireUserIdentifier()
    {Assert.False(new ApproveStudentRegistrationCommandValidator().Validate(new ApproveStudentRegistrationCommand(Guid.Empty)).IsValid);Assert.False(new RejectStudentRegistrationCommandValidator().Validate(new RejectStudentRegistrationCommand(Guid.Empty)).IsValid);}
    private sealed class Audit:IAuditLogRepository
    {public List<AuditLog> Values {get;}=[];public Task AddAsync(AuditLog value,CancellationToken token){Values.Add(value);return Task.CompletedTask;}}
    private sealed class RequestContext:IRequestContext{public string? IpAddress=>null;public string? TraceId=>"review-test";}
}
