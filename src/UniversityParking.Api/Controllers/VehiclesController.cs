using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UniversityParking.Api.Authorization;
using UniversityParking.Api.ExceptionHandling;
using UniversityParking.Api.Models;
using UniversityParking.Application.Common.Errors;
using UniversityParking.Application.Vehicles;
using UniversityParking.Contracts.Common;
using UniversityParking.Contracts.Vehicles;
using UniversityParking.Domain.Vehicles;

namespace UniversityParking.Api.Controllers;

[ApiController]
[Route("api/v1/vehicles")]
[Authorize(Policy = PolicyNames.Authenticated)]
[ProducesResponseType(typeof(ApiProblemDetails), 400)]
[ProducesResponseType(typeof(ApiProblemDetails), 401)]
[ProducesResponseType(typeof(ApiProblemDetails), 403)]
[ProducesResponseType(typeof(ApiProblemDetails), 404)]
[ProducesResponseType(typeof(ApiProblemDetails), 409)]
public sealed class VehiclesController(ISender sender) : ControllerBase
{
    [HttpPost]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(50 * 1024 * 1024)]
    [RequestFormLimits(MultipartBodyLengthLimit = 50 * 1024 * 1024)]
    [ProducesResponseType(typeof(VehicleCreatedResponse), 201)]
    public async Task<IActionResult> Register([FromForm] RegisterVehicleForm request, CancellationToken cancellationToken)
    {
        if (!VehicleMultipart.ValidFields(Request.Form, false)) return InvalidForm();
        var command = new RegisterVehicleCommand(VehicleMultipart.Parse<VehicleType>(request.Type), request.Plate, request.FrameNumber,
            request.Brand, request.Model, request.Color, VehicleMultipart.File(request.VerificationImage.File));
        var result = await sender.Send(command, cancellationToken);
        return result.IsSuccess ? CreatedAtAction(nameof(GetById), new { id = result.Value }, new VehicleCreatedResponse(result.Value)) :
            ProblemResponses.Map(HttpContext, result.Error!);
    }
    [HttpPost("{id:guid}/renew")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(50 * 1024 * 1024)]
    [RequestFormLimits(MultipartBodyLengthLimit = 50 * 1024 * 1024)]
    [ProducesResponseType(typeof(VehicleRenewedResponse), 201)]
    public async Task<IActionResult> Renew(Guid id, [FromForm] RenewVehicleForm? request, CancellationToken cancellationToken)
    {
        if (!VehicleMultipart.ValidFields(Request.Form, true)) return InvalidForm();
        request ??= new RenewVehicleForm();
        var result = await sender.Send(new RenewVehicleRegistrationCommand(id, request.Documents.Select(VehicleMultipart.Document).ToArray()), cancellationToken);
        return result.IsSuccess ? Created($"/api/v1/vehicles/{id:D}", new VehicleRenewedResponse(result.Value)) : ProblemResponses.Map(HttpContext, result.Error!);
    }
    [HttpGet("me")]
    [ProducesResponseType(typeof(VehicleResponse[]), 200)]
    public async Task<IActionResult> GetMine(CancellationToken cancellationToken)
    {
        var result = await sender.Send(new GetMyVehiclesQuery(), cancellationToken);
        return result.IsSuccess ? Ok(result.Value.Select(Map).ToArray()) : ProblemResponses.Map(HttpContext, result.Error!);
    }
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(VehicleDetailResponse), 200)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new GetVehicleByIdQuery(id), cancellationToken);
        if (result.IsFailure) return ProblemResponses.Map(HttpContext, result.Error!);
        var detail = result.Value;
        return Ok(new VehicleDetailResponse(Map(detail.Vehicle), detail.Photos.Select(x => new VehiclePhotoResponse(x.Id, x.Type.ToString(),
            x.OriginalFileName, x.ContentType, x.SizeBytes, PhotoUrl(id, x.Id))).ToArray(),
            detail.Documents.Select(x => new VehicleDocumentResponse(x.Id, x.Type.ToString(), x.DocumentNumber, x.OriginalFileName,
                x.ContentType, x.SizeBytes, x.IssuedOn, x.ExpiresOn, DocumentUrl(id, x.Id))).ToArray(),
            detail.VerificationImage is not { } image ? null : new VehicleVerificationImageResponse(image.Id, image.Type.ToString(),
                image.OriginalFileName, image.ContentType, image.SizeBytes, VerificationUrl(id))));
    }
    [HttpGet]
    [Authorize(Policy = PolicyNames.Admin)]
    [ProducesResponseType(typeof(PagedResponse<VehicleResponse>), 200)]
    public async Task<IActionResult> GetVehicles(CancellationToken cancellationToken, [FromQuery] string? search = null,
        [FromQuery] VehicleKind? type = null, [FromQuery] VehicleAccountStatus? status = null, [FromQuery] string? ownerIdentificationNumber = null,
        [FromQuery] VehicleRegistrationState? registrationState = null, [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        var result = await sender.Send(new GetVehiclesQuery(search, type.HasValue ? (VehicleType)type.Value : null,
            status.HasValue ? (VehicleStatus)status.Value : null, ownerIdentificationNumber,
            registrationState.HasValue ? (RegistrationState)registrationState.Value : null, page, pageSize), cancellationToken);
        if (result.IsFailure) return ProblemResponses.Map(HttpContext, result.Error!);
        var value = result.Value;
        return Ok(new PagedResponse<VehicleResponse>(value.Items.Select(Map).ToArray(), value.Page, value.PageSize, value.TotalCount, value.TotalPages));
    }
    [HttpPut("{id:guid}")]
    [ProducesResponseType(204)]
    public async Task<IActionResult> Update(Guid id, UpdateVehicleRequest request, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new UpdateVehicleCommand(id, request.Brand, request.Model, request.Color), cancellationToken);
        return result.IsSuccess ? NoContent() : ProblemResponses.Map(HttpContext, result.Error!);
    }
    [HttpPatch("{id:guid}/activate")]
    [ProducesResponseType(204)]
    public async Task<IActionResult> Activate(Guid id, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new ActivateVehicleCommand(id), cancellationToken);
        return result.IsSuccess ? NoContent() : ProblemResponses.Map(HttpContext, result.Error!);
    }
    [HttpPatch("{id:guid}/deactivate")]
    [ProducesResponseType(204)]
    public async Task<IActionResult> Deactivate(Guid id, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new DeactivateVehicleCommand(id), cancellationToken);
        return result.IsSuccess ? NoContent() : ProblemResponses.Map(HttpContext, result.Error!);
    }
    [HttpPatch("{id:guid}/identifier")]
    [Authorize(Policy = PolicyNames.Admin)]
    [ProducesResponseType(204)]
    public async Task<IActionResult> CorrectIdentifier(Guid id, CorrectVehicleIdentifierRequest request, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new CorrectVehicleIdentifierCommand(id, request.Identifier, request.Reason), cancellationToken);
        return result.IsSuccess ? NoContent() : ProblemResponses.Map(HttpContext, result.Error!);
    }
    [HttpPost("{id:guid}/transfer")]
    [Authorize(Policy = PolicyNames.Admin)]
    [ProducesResponseType(204)]
    public async Task<IActionResult> Transfer(Guid id, TransferVehicleRequest request, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new TransferVehicleCommand(id, request.NewOwnerIdentificationNumber, request.Reason), cancellationToken);
        return result.IsSuccess ? NoContent() : ProblemResponses.Map(HttpContext, result.Error!);
    }
    [HttpGet("{vehicleId:guid}/photos/{photoId:guid}/content")]
    [ProducesResponseType(typeof(byte[]), 200)]
    public async Task<IActionResult> PhotoContent(Guid vehicleId, Guid photoId, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new GetVehiclePhotoContentQuery(vehicleId, photoId), cancellationToken);
        return result.IsSuccess ? ContentResult(result.Value) : ProblemResponses.Map(HttpContext, result.Error!);
    }
    [HttpGet("{vehicleId:guid}/documents/{documentId:guid}/content")]
    [ProducesResponseType(typeof(byte[]), 200)]
    public async Task<IActionResult> DocumentContent(Guid vehicleId, Guid documentId, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new GetVehicleDocumentContentQuery(vehicleId, documentId), cancellationToken);
        return result.IsSuccess ? ContentResult(result.Value) : ProblemResponses.Map(HttpContext, result.Error!);
    }
    private IActionResult ContentResult(FileContent value)
    {
        Response.Headers.CacheControl = "private, no-store";
        Response.Headers["X-Content-Type-Options"] = "nosniff";
        return value.ReadUrl is not null ? Redirect(value.ReadUrl.AbsoluteUri) : File(value.Content!, value.ContentType, value.FileName);
    }
    private IActionResult InvalidForm() => ProblemResponses.Map(HttpContext, CommonErrors.Validation(
        new Dictionary<string, IReadOnlyList<string>> { ["request"] = ["Los campos no son válidos o sus índices no son consecutivos."] }));
    private static string PhotoUrl(Guid vehicleId, Guid photoId) => $"/api/v1/vehicles/{vehicleId:D}/photos/{photoId:D}/content";
    private static string DocumentUrl(Guid vehicleId, Guid documentId) => $"/api/v1/vehicles/{vehicleId:D}/documents/{documentId:D}/content";
    private static string VerificationUrl(Guid vehicleId) => $"/api/v1/vehicles/{vehicleId:D}/verification-image/content";
    [HttpPut("{id:guid}/verification-image")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(50 * 1024 * 1024)]
    [RequestFormLimits(MultipartBodyLengthLimit = 50 * 1024 * 1024)]
    [ProducesResponseType(204)]
    public async Task<IActionResult> UpdateVerificationImage(Guid id, [FromForm] UpdateVerificationImageForm request, CancellationToken cancellationToken)
    {
        if (!VehicleMultipart.VerificationFields(Request.Form) || Request.Form.Keys.Count != 0) return InvalidForm();
        var result = await sender.Send(new UpdateVehicleVerificationImageCommand(id, VehicleMultipart.File(request.VerificationImage.File)), cancellationToken);
        return result.IsSuccess ? NoContent() : ProblemResponses.Map(HttpContext, result.Error!);
    }
    [HttpGet("{id:guid}/verification-image/content")]
    [ProducesResponseType(typeof(byte[]), 200)]
    public async Task<IActionResult> VerificationContent(Guid id, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new GetVehicleVerificationImageContentQuery(id), cancellationToken);
        return result.IsSuccess ? ContentResult(result.Value) : ProblemResponses.Map(HttpContext, result.Error!);
    }
    private static VehicleResponse Map(VehicleView x) => new(x.Id, x.Type.ToString(), x.Plate, x.FrameNumber, x.Brand, x.Model, x.Color,
        x.Status.ToString(), x.CurrentOwnerId, x.CurrentOwnerFullName, x.RegistrationState.ToString(), x.IsInside,
        x.VerificationImageId.HasValue ? VerificationUrl(x.Id) : null);
}
