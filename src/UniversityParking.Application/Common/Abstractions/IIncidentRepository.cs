using UniversityParking.Domain.Incidents;
using UniversityParking.Application.Incidents;
using UniversityParking.Application.Common.Pagination;

namespace UniversityParking.Application.Common.Abstractions;

public interface IIncidentRepository
{
    Task<Incident?> GetByIdAsync(Guid id, CancellationToken cancellationToken);
    Task AddAsync(Incident incident, CancellationToken cancellationToken);
    Task<Incident?> GetByIdForUpdateAsync(Guid id, CancellationToken cancellationToken);
    Task AddAttachmentAsync(IncidentAttachment attachment, CancellationToken cancellationToken);
    Task<IReadOnlyList<IncidentAttachment>> GetAttachmentsAsync(Guid incidentId, CancellationToken cancellationToken);
    Task<IncidentAttachment?> GetAttachmentAsync(Guid incidentId, Guid attachmentId, CancellationToken cancellationToken);
    Task<PagedResult<Incident>> SearchAsync(IncidentFilter filter, CancellationToken cancellationToken);
}
