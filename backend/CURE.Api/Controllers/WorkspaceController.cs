using CURE.Application.Shared;
using CURE.Application.Workspace;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CURE.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/v1")]
public sealed class WorkspaceController(IWorkspaceService workspace, IRequestContextAccessor requestContext) : ControllerBase
{
    [HttpGet("home/dashboard")]
    public async Task<IActionResult> Dashboard(CancellationToken ct)
    {
        var current = requestContext.Current;
        return Ok(new { data = await workspace.GetDashboardAsync(current.TenantId, current.UserId, ct) });
    }

    [HttpGet("search")]
    public async Task<IActionResult> Search(
        [FromQuery] string query,
        [FromQuery] int limit = 20,
        CancellationToken ct = default)
    {
        var tenantId = requestContext.Current.TenantId;
        return Ok(new { data = await workspace.SearchAsync(tenantId, query, limit, ct) });
    }

    [HttpGet("notifications")]
    public async Task<IActionResult> Notifications([FromQuery] int limit = 20, CancellationToken ct = default)
    {
        var current = requestContext.Current;
        return Ok(new { data = await workspace.GetNotificationsAsync(current.TenantId, current.UserId, limit, ct) });
    }

    [HttpPost("notifications/{entityType}/{id:guid}/read")]
    public async Task<IActionResult> MarkNotificationRead(string entityType, Guid id, CancellationToken ct)
    {
        var current = requestContext.Current;
        await workspace.SetNotificationReadAsync(current.TenantId, current.UserId, entityType, id, true, ct);
        return NoContent();
    }

    [HttpDelete("notifications/{entityType}/{id:guid}/read")]
    public async Task<IActionResult> MarkNotificationUnread(string entityType, Guid id, CancellationToken ct)
    {
        var current = requestContext.Current;
        await workspace.SetNotificationReadAsync(current.TenantId, current.UserId, entityType, id, false, ct);
        return NoContent();
    }
}
