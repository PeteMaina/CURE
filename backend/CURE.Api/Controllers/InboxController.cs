using CURE.Application.Shared;
using CURE.Application.Workspace;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CURE.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/inbox")]
public sealed class InboxController(IWorkspaceService workspace, IRequestContextAccessor requestContext) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] string? category,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        CancellationToken ct = default)
    {
        var current = requestContext.Current;
        var result = await workspace.GetInboxAsync(
            current.TenantId,
            current.UserId,
            category,
            page,
            pageSize,
            ct);

        return Ok(new
        {
            data = result.Data,
            meta = new
            {
                page = result.Page,
                pageSize = result.PageSize,
                total = result.Total,
                dueActivityCount = result.DueActivityCount,
                openCaseCount = result.OpenCaseCount,
            },
        });
    }
}
