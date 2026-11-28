namespace CURE.Api.Controllers;

using CURE.Application.Engagement;
using CURE.Application.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

[ApiController]
[Authorize]
[Route("api/v1")]
public sealed class ActivityController(
    IActivityService activityService,
    IRequestContextAccessor requestContext) : ControllerBase
{
    /// <summary>
    /// Log a new activity (call, email, meeting, note, task, demo, etc.) against
    /// any combination of customer, contact, or opportunity.
    /// </summary>
    [Authorize(Policy = "SalesWrite")]
    [HttpPost("activities")]
    public async Task<IActionResult> LogActivity(
        [FromBody] LogActivityCommand command,
        CancellationToken ct)
    {
        var current  = requestContext.Current;
        var activity = await activityService.LogActivityAsync(
            current.TenantId, current.UserId, current.RequestId, command, ct);
        return Ok(new { data = activity });
    }

    /// <summary>
    /// Mark an activity complete, recording the outcome and optional actual duration.
    /// For outcome-required types (Call, Meeting, Demo, Task) the outcome field is mandatory.
    /// </summary>
    [Authorize(Policy = "SalesWrite")]
    [HttpPatch("activities/{id:guid}/complete")]
    public async Task<IActionResult> CompleteActivity(
        Guid id,
        [FromBody] CompleteActivityCommand command,
        CancellationToken ct)
    {
        var current  = requestContext.Current;
        var activity = await activityService.CompleteActivityAsync(
            current.TenantId, current.UserId, current.RequestId, id, command, ct);
        return Ok(new { data = activity });
    }

    /// <summary>
    /// Cancel a planned or in-progress activity with an optional reason.
    /// </summary>
    [Authorize(Policy = "SalesWrite")]
    [HttpPatch("activities/{id:guid}/cancel")]
    public async Task<IActionResult> CancelActivity(
        Guid id,
        [FromBody] CancelActivityCommand command,
        CancellationToken ct)
    {
        var current  = requestContext.Current;
        var activity = await activityService.CancelActivityAsync(
            current.TenantId, current.UserId, id, command, ct);
        return Ok(new { data = activity });
    }

    /// <summary>
    /// Unified customer timeline: activities, opportunity stage transitions, and
    /// audit events merged newest-first with keyset pagination via the
    /// <c>before</c> cursor (an ISO-8601 timestamp).
    /// </summary>
    [HttpGet("customers/{customerId:guid}/timeline")]
    public async Task<IActionResult> GetCustomerTimeline(
        Guid customerId,
        [FromQuery] int limit = 25,
        [FromQuery] DateTimeOffset? before = null,
        CancellationToken ct = default)
    {
        var current  = requestContext.Current;
        var timeline = await activityService.GetCustomerTimelineAsync(
            current.TenantId, customerId, limit, before, ct);
        return Ok(new { data = timeline });
    }
}
