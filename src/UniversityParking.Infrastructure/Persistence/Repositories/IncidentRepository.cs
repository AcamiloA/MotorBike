using Microsoft.EntityFrameworkCore;
using UniversityParking.Application.Common.Abstractions;
using UniversityParking.Domain.Incidents;
using UniversityParking.Application.Incidents;
using UniversityParking.Application.Common.Pagination;

namespace UniversityParking.Infrastructure.Persistence.Repositories;

public sealed class IncidentRepository(AppDbContext context) : IIncidentRepository
{
    public Task<Incident?> GetByIdAsync(Guid id, CancellationToken cancellationToken) => context.Incidents.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
    public async Task AddAsync(Incident incident, CancellationToken cancellationToken) => await context.Incidents.AddAsync(incident, cancellationToken);
    public async Task<Incident?> GetByIdForUpdateAsync(Guid id, CancellationToken cancellationToken)
    {
        var incident = await context.Incidents.FromSqlInterpolated($"SELECT * FROM incidents WHERE id = {id} FOR UPDATE").SingleOrDefaultAsync(cancellationToken);
        if (incident is not null) await context.Entry(incident).ReloadAsync(cancellationToken);
        return incident;
    }
    public async Task AddAttachmentAsync(IncidentAttachment attachment, CancellationToken cancellationToken) =>
        await context.IncidentAttachments.AddAsync(attachment, cancellationToken);
    public async Task<IReadOnlyList<IncidentAttachment>> GetAttachmentsAsync(Guid incidentId, CancellationToken cancellationToken) =>
        await context.IncidentAttachments.AsNoTracking().Where(x => x.IncidentId == incidentId).OrderBy(x => x.CreatedAt).ThenBy(x => x.Id).ToListAsync(cancellationToken);
    public Task<IncidentAttachment?> GetAttachmentAsync(Guid incidentId, Guid attachmentId, CancellationToken cancellationToken) =>
        context.IncidentAttachments.AsNoTracking().SingleOrDefaultAsync(x => x.IncidentId == incidentId && x.Id == attachmentId, cancellationToken);
    public async Task<PagedResult<Incident>> SearchAsync(IncidentFilter filter, CancellationToken cancellationToken)
    {
        await using var transaction = await context.Database.BeginTransactionAsync(System.Data.IsolationLevel.RepeatableRead, cancellationToken);
        var query = context.Incidents.AsNoTracking();
        if (filter.Status.HasValue) query = query.Where(x => x.Status == filter.Status.Value);
        if (filter.Type.HasValue) query = query.Where(x => x.Type == filter.Type.Value);
        if (filter.ParkingLotId.HasValue) query = query.Where(x => x.ParkingLotId == filter.ParkingLotId.Value);
        if (filter.UserId.HasValue) query = query.Where(x => x.UserId == filter.UserId.Value);
        if (filter.VehicleId.HasValue) query = query.Where(x => x.VehicleId == filter.VehicleId.Value);
        if (filter.FromUtc.HasValue) query = query.Where(x => x.OccurredAt >= filter.FromUtc.Value);
        if (filter.UntilUtc.HasValue) query = query.Where(x => x.OccurredAt < filter.UntilUtc.Value);
        var count = await query.LongCountAsync(cancellationToken);
        var offset = ((long)filter.Page - 1) * filter.PageSize;
        var items = offset >= count ? [] : await query.OrderByDescending(x => x.OccurredAt).ThenBy(x => x.Id)
            .Skip(checked((int)offset)).Take(filter.PageSize).ToListAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new(items, filter.Page, filter.PageSize, count);
    }
}
