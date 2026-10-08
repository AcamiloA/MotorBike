using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UniversityParking.Api.Authorization;
using UniversityParking.Api.ExceptionHandling;
using UniversityParking.Application.News;
using UniversityParking.Contracts.Common;
using UniversityParking.Contracts.News;
using UniversityParking.Domain.News;

namespace UniversityParking.Api.Controllers;

[ApiController]
[Route("api/v1")]
[Authorize(Policy = PolicyNames.Authenticated)]
[ProducesResponseType(typeof(ApiProblemDetails), 400)]
[ProducesResponseType(typeof(ApiProblemDetails), 401)]
[ProducesResponseType(typeof(ApiProblemDetails), 403)]
[ProducesResponseType(typeof(ApiProblemDetails), 404)]
[ProducesResponseType(typeof(ApiProblemDetails), 409)]
public sealed class NewsController(ISender sender) : ControllerBase
{
    [HttpGet("news")]
    [ProducesResponseType(typeof(PagedResponse<NewsResponse>), 200)]
    public async Task<IActionResult> Published(CancellationToken cancellationToken, [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        var result = await sender.Send(new GetPublishedNewsQuery(page, pageSize), cancellationToken);
        return result.IsSuccess ? Ok(Map(result.Value)) : ProblemResponses.Map(HttpContext, result.Error!);
    }
    [HttpGet("admin/news")]
    [Authorize(Policy = PolicyNames.Admin)]
    [ProducesResponseType(typeof(PagedResponse<NewsResponse>), 200)]
    public async Task<IActionResult> AdminList(CancellationToken cancellationToken, [FromQuery] NewsState? status = null,
        [FromQuery] string? search = null, [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        var result = await sender.Send(new GetAdminNewsQuery(status.HasValue ? (NewsStatus)status.Value : null, search, page, pageSize), cancellationToken);
        return result.IsSuccess ? Ok(Map(result.Value)) : ProblemResponses.Map(HttpContext, result.Error!);
    }
    [HttpPost("admin/news")]
    [Authorize(Policy = PolicyNames.Admin)]
    [ProducesResponseType(typeof(NewsCreatedResponse), 201)]
    public async Task<IActionResult> Create(CreateNewsRequest request, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new CreateNewsCommand(request.Title, request.Content), cancellationToken);
        return result.IsSuccess ? Created("/api/v1/admin/news", new NewsCreatedResponse(result.Value)) : ProblemResponses.Map(HttpContext, result.Error!);
    }
    [HttpPut("admin/news/{id:guid}")]
    [Authorize(Policy = PolicyNames.Admin)]
    [ProducesResponseType(204)]
    public async Task<IActionResult> Update(Guid id, UpdateNewsRequest request, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new UpdateNewsCommand(id, request.Title, request.Content), cancellationToken);
        return result.IsSuccess ? NoContent() : ProblemResponses.Map(HttpContext, result.Error!);
    }
    [HttpPost("admin/news/{id:guid}/publish")]
    [Authorize(Policy = PolicyNames.Admin)]
    [ProducesResponseType(204)]
    public async Task<IActionResult> Publish(Guid id, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new PublishNewsCommand(id), cancellationToken);
        return result.IsSuccess ? NoContent() : ProblemResponses.Map(HttpContext, result.Error!);
    }
    [HttpPost("admin/news/{id:guid}/archive")]
    [Authorize(Policy = PolicyNames.Admin)]
    [ProducesResponseType(204)]
    public async Task<IActionResult> Archive(Guid id, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new ArchiveNewsCommand(id), cancellationToken);
        return result.IsSuccess ? NoContent() : ProblemResponses.Map(HttpContext, result.Error!);
    }
    private static PagedResponse<NewsResponse> Map(UniversityParking.Application.Common.Pagination.PagedResult<NewsView> page) =>
        new(page.Items.Select(x => new NewsResponse(x.Id, x.Title, x.Content, x.Status.ToString(), x.PublishedAt,
            x.ArchivedAt, x.CreatedBy, x.CreatedAt, x.UpdatedAt)).ToArray(), page.Page, page.PageSize, page.TotalCount, page.TotalPages);
}
