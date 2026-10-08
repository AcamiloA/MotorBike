using Microsoft.EntityFrameworkCore;
using UniversityParking.Application.Common.Abstractions;
using UniversityParking.Domain.AcademicPeriods;

namespace UniversityParking.Infrastructure.Persistence.Repositories;

public sealed class AcademicPeriodRepository(AppDbContext context) : IAcademicPeriodRepository
{
    public async Task<AcademicPeriod?> GetByIdForUpdateAsync(Guid id, CancellationToken cancellationToken)
    {
        var period = await context.AcademicPeriods.FromSqlInterpolated($"SELECT * FROM academic_periods WHERE id = {id} FOR UPDATE").SingleOrDefaultAsync(cancellationToken);
        if (period is not null) await context.Entry(period).ReloadAsync(cancellationToken);
        return period;
    }
    public async Task<IReadOnlyList<AcademicPeriod>> GetAllAsync(CancellationToken cancellationToken) =>
        await context.AcademicPeriods.AsNoTracking().OrderByDescending(x => x.StartsOn).ThenBy(x => x.Id).ToListAsync(cancellationToken);
    public Task<bool> ExistsByNameAsync(string name, CancellationToken cancellationToken) => context.AcademicPeriods.AnyAsync(x => x.Name == name, cancellationToken);
    public Task<AcademicPeriod?> GetActiveForShareAsync(CancellationToken cancellationToken) =>
        context.AcademicPeriods.FromSqlRaw("SELECT * FROM academic_periods WHERE status = 'ACTIVE' FOR SHARE").SingleOrDefaultAsync(cancellationToken);
    public Task<AcademicPeriod?> GetByIdAsync(Guid id, CancellationToken cancellationToken) => context.AcademicPeriods.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
    public Task<AcademicPeriod?> GetActiveAsync(CancellationToken cancellationToken) => context.AcademicPeriods.FirstOrDefaultAsync(x => x.Status == AcademicPeriodStatus.ACTIVE, cancellationToken);
    public Task<bool> ExistsActiveAsync(CancellationToken cancellationToken) => context.AcademicPeriods.AnyAsync(x => x.Status == AcademicPeriodStatus.ACTIVE, cancellationToken);
    public async Task AddAsync(AcademicPeriod period, CancellationToken cancellationToken) => await context.AcademicPeriods.AddAsync(period, cancellationToken);
}
