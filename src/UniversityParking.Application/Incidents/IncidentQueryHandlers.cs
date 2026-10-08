using MediatR;
using UniversityParking.Application.Common.Abstractions;
using UniversityParking.Application.Common.Authorization;
using UniversityParking.Application.Common.Errors;
using UniversityParking.Application.Common.Pagination;
using UniversityParking.Application.Common.Results;
using UniversityParking.Application.Vehicles;
using UniversityParking.Domain.Incidents;
using UniversityParking.Domain.Users;

namespace UniversityParking.Application.Incidents;

public sealed class GetIncidentByIdQueryHandler(AdministrationOperationContext operation, IIncidentRepository incidents)
    : IRequestHandler<GetIncidentByIdQuery, Result<IncidentDetail>>
{
    public async Task<Result<IncidentDetail>> Handle(GetIncidentByIdQuery request, CancellationToken cancellationToken)
    {
        if (await operation.CheckAccessAsync([RoleCodes.Guard, RoleCodes.Admin], cancellationToken) is { } permission) return Result<IncidentDetail>.Failure(permission);
        var incident = await incidents.GetByIdAsync(request.IncidentId, cancellationToken);
        return incident is null ? Result<IncidentDetail>.Failure(IncidentErrors.NotFound) :
            Result<IncidentDetail>.Success(new(incident, await incidents.GetAttachmentsAsync(incident.Id, cancellationToken)));
    }
}
public sealed class GetIncidentsQueryHandler(AdministrationOperationContext operation, IIncidentRepository incidents, IParkingTimeZone timeZone)
    : IRequestHandler<GetIncidentsQuery, Result<PagedResult<Incident>>>
{
    public async Task<Result<PagedResult<Incident>>> Handle(GetIncidentsQuery request, CancellationToken cancellationToken)
    {
        if (await operation.CheckAccessAsync([RoleCodes.Guard, RoleCodes.Admin], cancellationToken) is { } permission) return Result<PagedResult<Incident>>.Failure(permission);
        return Result<PagedResult<Incident>>.Success(await incidents.SearchAsync(new(request.Status, request.Type, request.ParkingLotId,
            request.UserId, request.VehicleId, request.DateFrom.HasValue ? timeZone.GetUtcStartOfDay(request.DateFrom.Value) : null,
            request.DateTo.HasValue && request.DateTo.Value != DateOnly.MaxValue ? timeZone.GetUtcStartOfDay(request.DateTo.Value.AddDays(1)) : null,
            request.Page, request.PageSize), cancellationToken));
    }
}
public sealed class GetIncidentAttachmentContentQueryHandler(AdministrationOperationContext operation, IIncidentRepository incidents,
    IFileStorage storage) : IRequestHandler<GetIncidentAttachmentContentQuery, Result<FileContent>>
{
    public async Task<Result<FileContent>> Handle(GetIncidentAttachmentContentQuery request, CancellationToken cancellationToken)
    {
        if (await operation.CheckAccessAsync([RoleCodes.Guard, RoleCodes.Admin], cancellationToken) is { } permission) return Result<FileContent>.Failure(permission);
        var file = await incidents.GetAttachmentAsync(request.IncidentId, request.AttachmentId, cancellationToken);
        if (file is null) return Result<FileContent>.Failure(IncidentErrors.NotFound);
        var url = await storage.GetReadUrlAsync(file.StorageKey, cancellationToken);
        if (url is not null) return Result<FileContent>.Success(new(null, url, file.ContentType, file.OriginalFileName));
        try { return Result<FileContent>.Success(new(await storage.OpenReadAsync(file.StorageKey, cancellationToken), null, file.ContentType, file.OriginalFileName)); }
        catch (FileNotFoundException) { return Result<FileContent>.Failure(IncidentErrors.NotFound); }
        catch (DirectoryNotFoundException) { return Result<FileContent>.Failure(IncidentErrors.NotFound); }
    }
}
