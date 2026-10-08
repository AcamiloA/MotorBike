using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UniversityParking.Api.Authorization;
using UniversityParking.Api.ExceptionHandling;
using UniversityParking.Api.Models;
using UniversityParking.Application.Common.Errors;
using UniversityParking.Application.Incidents;
using UniversityParking.Contracts.Common;
using UniversityParking.Contracts.Incidents;
using UniversityParking.Domain.Incidents;

namespace UniversityParking.Api.Controllers;

[ApiController]
[Route("api/v1/incidents")]
[Authorize(Policy = PolicyNames.GuardOrAdmin)]
[ProducesResponseType(typeof(ApiProblemDetails), 400)]
[ProducesResponseType(typeof(ApiProblemDetails), 401)]
[ProducesResponseType(typeof(ApiProblemDetails), 403)]
[ProducesResponseType(typeof(ApiProblemDetails), 404)]
[ProducesResponseType(typeof(ApiProblemDetails), 409)]
public sealed class IncidentsController(ISender sender) : ControllerBase
{
    [HttpPost]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(50 * 1024 * 1024)]
    [RequestFormLimits(MultipartBodyLengthLimit = 50 * 1024 * 1024)]
    [ProducesResponseType(typeof(IncidentCreatedResponse), 201)]
    public async Task<IActionResult> Create([FromForm] IncidentForm request, CancellationToken cancellationToken)
    {
        if (!IncidentMultipart.Valid(Request.Form)) return ProblemResponses.Map(HttpContext, CommonErrors.Validation(
            new Dictionary<string, IReadOnlyList<string>> { ["request"] = ["Los campos o índices de adjuntos no son válidos."] }));
        var result = await sender.Send(new CreateIncidentCommand(request.ParkingLotId, VehicleMultipart.Parse<IncidentType>(request.Type),
            request.Description, request.UserId, request.VehicleId, request.ParkingMovementId, request.OccurredAt,
            Request.Form.Files.OrderBy(x => int.Parse(x.Name[(x.Name.IndexOf('[') + 1)..^1])).Select(VehicleMultipart.File).ToArray()), cancellationToken);
        return result.IsSuccess ? CreatedAtAction(nameof(GetById), new { id = result.Value }, new IncidentCreatedResponse(result.Value)) : ProblemResponses.Map(HttpContext, result.Error!);
    }
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(IncidentDetailResponse), 200)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new GetIncidentByIdQuery(id), cancellationToken);
        return result.IsSuccess ? Ok(new IncidentDetailResponse(Map(result.Value.Incident), result.Value.Attachments.Select(x =>
            new IncidentAttachmentResponse(x.Id, x.OriginalFileName, x.ContentType, x.SizeBytes, $"/api/v1/incidents/{id:D}/attachments/{x.Id:D}/content")).ToArray())) :
            ProblemResponses.Map(HttpContext, result.Error!);
    }
    [HttpGet]
    [ProducesResponseType(typeof(PagedResponse<IncidentResponse>), 200)]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken, [FromQuery] IncidentState? status = null,
        [FromQuery] IncidentKind? type = null, [FromQuery] Guid? parkingLotId = null, [FromQuery] Guid? userId = null,
        [FromQuery] Guid? vehicleId = null, [FromQuery] DateOnly? dateFrom = null, [FromQuery] DateOnly? dateTo = null,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        var result = await sender.Send(new GetIncidentsQuery(status.HasValue ? (IncidentStatus)status.Value : null,
            type.HasValue ? (IncidentType)type.Value : null, parkingLotId, userId, vehicleId, dateFrom, dateTo, page, pageSize), cancellationToken);
        if (result.IsFailure) return ProblemResponses.Map(HttpContext, result.Error!);
        var value = result.Value;
        return Ok(new PagedResponse<IncidentResponse>(value.Items.Select(Map).ToArray(), value.Page, value.PageSize, value.TotalCount, value.TotalPages));
    }
    [HttpPost("{id:guid}/resolve")]
    [Authorize(Policy = PolicyNames.Admin)]
    [ProducesResponseType(204)]
    public async Task<IActionResult> Resolve(Guid id, ResolveIncidentRequest request, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new ResolveIncidentCommand(id, request.Resolution), cancellationToken);
        return result.IsSuccess ? NoContent() : ProblemResponses.Map(HttpContext, result.Error!);
    }
    [HttpPost("{id:guid}/cancel")]
    [Authorize(Policy = PolicyNames.Admin)]
    [ProducesResponseType(204)]
    public async Task<IActionResult> Cancel(Guid id, [FromBody(EmptyBodyBehavior = Microsoft.AspNetCore.Mvc.ModelBinding.EmptyBodyBehavior.Allow)] CancelIncidentRequest? request,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(new CancelIncidentCommand(id, request?.Reason), cancellationToken);
        return result.IsSuccess ? NoContent() : ProblemResponses.Map(HttpContext, result.Error!);
    }
    [HttpGet("{incidentId:guid}/attachments/{attachmentId:guid}/content")]
    [ProducesResponseType(typeof(byte[]), 200)]
    [ProducesResponseType(302)]
    public async Task<IActionResult> Attachment(Guid incidentId, Guid attachmentId, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new GetIncidentAttachmentContentQuery(incidentId, attachmentId), cancellationToken);
        if (result.IsFailure) return ProblemResponses.Map(HttpContext, result.Error!);
        Response.Headers.CacheControl = "private, no-store";
        Response.Headers["X-Content-Type-Options"] = "nosniff";
        var file = result.Value;
        return file.ReadUrl is not null ? Redirect(file.ReadUrl.AbsoluteUri) : File(file.Content!, file.ContentType, file.FileName);
    }
    private static IncidentResponse Map(Incident x) => new(x.Id, x.ParkingLotId, x.UserId, x.VehicleId, x.ParkingMovementId,
        x.ReportedBy, x.Type.ToString(), x.Description, x.Status.ToString(), x.OccurredAt, x.Resolution, x.ResolvedBy, x.ResolvedAt, x.CreatedAt, x.UpdatedAt);
}
