using UniversityParking.Application.AcademicPeriods;
using UniversityParking.Domain.AcademicPeriods;

namespace UniversityParking.Application.Tests.Administration;

public sealed class AcademicPeriodTests
{
    private readonly AdministrationTestContext context = new();
    private CreateAcademicPeriodCommandHandler Create => new(context.Operation, context, context.Work);
    private ActivateAcademicPeriodCommandHandler Activate => new(context.Operation, context, context.Work);
    private CloseAcademicPeriodCommandHandler Close => new(context.Operation, context, context.Work);
    private static CreateAcademicPeriodCommand Request(string name = "2027-1") => new(name, new(2027, 1, 15), new(2027, 6, 30));
    [Fact]
    public async Task CreateStartsPlannedAndAuditsWithActorAndTrace()
    {
        var result = await Create.Handle(Request(), default);
        Assert.True(result.IsSuccess);
        Assert.Equal(AcademicPeriodStatus.PLANNED, Assert.Single(context.Periods).Status);
        var audit = Assert.Single(context.Audits);
        Assert.Equal("ACADEMIC_PERIOD_CREATED", audit.Action);
        Assert.Equal(context.Actor.UserId, audit.ActorUserId);
        Assert.Equal("administration-test", audit.TraceId);
        Assert.Equal(1, context.Work.SaveCount);
    }
    [Theory]
    [InlineData("USER")]
    [InlineData("GUARD")]
    public async Task CreateRequiresAdmin(string role)
    {
        context.Actor.Roles = [role];
        Assert.Equal("FORBIDDEN", (await Create.Handle(Request(), default)).Error!.Code);
        Assert.Empty(context.Periods);
    }
    [Fact]
    public async Task RemovedAdminRoleCannotUseOldClaims()
    {
        context.Roles.Codes = ["USER"];
        Assert.Equal("FORBIDDEN", (await Create.Handle(Request(), default)).Error!.Code);
    }
    [Fact]
    public async Task DuplicateNormalizedNameIsRejected()
    {
        await Create.Handle(Request(), default);
        Assert.Equal("ACADEMIC_PERIOD_ALREADY_EXISTS", (await Create.Handle(Request(" 2027-1 "), default)).Error!.Code);
        Assert.Single(context.Periods);
    }
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void InvalidDateRangesAreRejected(int days)
    {
        var request = Request();
        Assert.False(new CreateAcademicPeriodCommandValidator().Validate(request with { EndsOn = request.StartsOn.AddDays(days) }).IsValid);
    }
    [Fact]
    public async Task ActivateAndCloseAreIdempotent_ClosedCannotReactivate()
    {
        var id = (await Create.Handle(Request(), default)).Value;
        Assert.True((await Activate.Handle(new(id), default)).IsSuccess);
        Assert.True((await Activate.Handle(new(id), default)).IsSuccess);
        Assert.True((await Close.Handle(new(id), default)).IsSuccess);
        Assert.True((await Close.Handle(new(id), default)).IsSuccess);
        Assert.Equal("ACADEMIC_PERIOD_NOT_ACTIVE", (await Activate.Handle(new(id), default)).Error!.Code);
        Assert.Equal(3, context.Work.SaveCount);
        Assert.Equal(1, context.Audits.Count(x => x.Action == "ACADEMIC_PERIOD_ACTIVATED"));
        Assert.Equal(1, context.Audits.Count(x => x.Action == "ACADEMIC_PERIOD_CLOSED"));
    }
    [Fact]
    public async Task CannotActivateSecondPeriodOrClosePlannedPeriod()
    {
        var first = (await Create.Handle(Request(), default)).Value;
        var second = (await Create.Handle(Request("2027-2"), default)).Value;
        await Activate.Handle(new(first), default);
        Assert.Equal("ACTIVE_ACADEMIC_PERIOD_ALREADY_EXISTS", (await Activate.Handle(new(second), default)).Error!.Code);
        Assert.Equal("ACADEMIC_PERIOD_NOT_ACTIVE", (await Close.Handle(new(second), default)).Error!.Code);
        await Close.Handle(new(first), default);
        Assert.Equal(AcademicPeriodStatus.PLANNED, context.Periods.Single(x => x.Id == second).Status);
    }
    [Fact]
    public async Task CurrentPeriodIsAvailableToAuthenticatedUser_AbsenceIs404()
    {
        context.Actor.Roles = ["USER"];
        var query = new GetCurrentAcademicPeriodQueryHandler(context.Operation, context);
        var absent = await query.Handle(new(), default);
        Assert.Equal(Application.Common.Results.ErrorType.NotFound, absent.Error!.Type);
        var period = new AcademicPeriod("2026-2", new(2026, 7, 1), new(2026, 12, 31), context.Clock.UtcNow);
        period.Activate();
        context.Periods.Add(period);
        Assert.Equal(period.Id, (await query.Handle(new(), default)).Value.Id);
    }
    [Fact]
    public async Task MissingPeriodReturnsNotFound()
    {
        Assert.Equal("ACADEMIC_PERIOD_NOT_FOUND", (await Activate.Handle(new(Guid.NewGuid()), default)).Error!.Code);
        Assert.Equal("ACADEMIC_PERIOD_NOT_FOUND", (await Close.Handle(new(Guid.NewGuid()), default)).Error!.Code);
    }
}
