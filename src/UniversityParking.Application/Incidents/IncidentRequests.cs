using UniversityParking.Application.Common.Messaging;
using UniversityParking.Application.Common.Pagination;
using UniversityParking.Application.Files;
using UniversityParking.Domain.Incidents;

namespace UniversityParking.Application.Incidents;

public sealed record CreateIncidentCommand(Guid ParkingLotId, IncidentType Type, string Description,
    Guid? UserId = null, Guid? VehicleId = null, Guid? ParkingMovementId = null,
    DateTimeOffset? OccurredAt = null, IReadOnlyList<UploadSource>? Attachments = null) : ICommand<Guid>;
public sealed record ResolveIncidentCommand(Guid IncidentId, string Resolution) : ICommand;
public sealed record CancelIncidentCommand(Guid IncidentId, string? Reason = null) : ICommand;
public sealed record GetIncidentByIdQuery(Guid IncidentId) : IQuery<IncidentDetail>;
public sealed record GetIncidentAttachmentContentQuery(Guid IncidentId, Guid AttachmentId) : IQuery<UniversityParking.Application.Vehicles.FileContent>;
public sealed record GetIncidentsQuery(IncidentStatus? Status = null, IncidentType? Type = null, Guid? ParkingLotId = null,
    Guid? UserId = null, Guid? VehicleId = null, DateOnly? DateFrom = null, DateOnly? DateTo = null,
    int Page = 1, int PageSize = 20) : IQuery<PagedResult<Incident>>;
public sealed record IncidentDetail(Incident Incident, IReadOnlyList<IncidentAttachment> Attachments);
public sealed record IncidentFilter(IncidentStatus? Status, IncidentType? Type, Guid? ParkingLotId, Guid? UserId,
    Guid? VehicleId, DateTimeOffset? FromUtc, DateTimeOffset? UntilUtc, int Page, int PageSize);
