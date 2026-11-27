namespace CURE.Api.Controllers;

using CURE.Application.Pipeline;
using CURE.Application.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

[ApiController]
[Authorize]
[Route("api/v1")]
public sealed class PipelineController(
    IPipelineService pipelineService,
    IRequestContextAccessor requestContext) : ControllerBase
{
    [HttpGet("pipeline/summary")]
    public async Task<IActionResult> GetSummary(CancellationToken ct)
    {
        var current = requestContext.Current;
        var summary = await pipelineService.GetPipelineSummaryAsync(current.TenantId, ct);
        return Ok(new { data = summary });
    }

    [HttpGet("opportunities/{id:guid}/history")]
    public async Task<IActionResult> GetStageHistory(Guid id, CancellationToken ct)
    {
        var current = requestContext.Current;
        var history = await pipelineService.GetStageHistoryAsync(current.TenantId, id, ct);
        return Ok(new { data = history });
    }

    [Authorize(Policy = "SalesWrite")]
    [HttpPost("opportunities/{id:guid}/transition")]
    public async Task<IActionResult> TransitionStage(
        Guid id,
        [FromBody] TransitionStageCommand command,
        CancellationToken ct)
    {
        var current = requestContext.Current;
        var updated = await pipelineService.TransitionStageAsync(
            current.TenantId,
            current.UserId,
            current.RequestId,
            id,
            command,
            ct);

        return Ok(new { data = updated });
    }

    [Authorize(Policy = "SalesWrite")]
    [HttpPost("leads/{id:guid}/convert")]
    public async Task<IActionResult> ConvertLead(
        Guid id,
        [FromBody] ConvertLeadCommand command,
        CancellationToken ct)
    {
        var current = requestContext.Current;
        var result = await pipelineService.ConvertLeadAsync(
            current.TenantId,
            current.UserId,
            current.RequestId,
            id,
            command,
            ct);

        return Ok(new { data = result });
    }
}
