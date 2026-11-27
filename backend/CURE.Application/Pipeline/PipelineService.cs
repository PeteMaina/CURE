namespace CURE.Application.Pipeline;

using CURE.Application.CoreCrm;
using CURE.Domain.Shared;

public sealed class PipelineService(IPipelineStore store) : IPipelineService
{
    public Task<PipelineSummaryDto> GetPipelineSummaryAsync(Guid tenantId, CancellationToken ct)
        => store.GetPipelineSummaryAsync(tenantId, ct);

    public Task<IReadOnlyList<OpportunityStageHistoryDto>> GetStageHistoryAsync(Guid tenantId, Guid opportunityId, CancellationToken ct)
        => store.GetStageHistoryAsync(tenantId, opportunityId, ct);

    public async Task<OpportunityDto> TransitionStageAsync(
        Guid tenantId,
        Guid actorId,
        string requestId,
        Guid opportunityId,
        TransitionStageCommand command,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(command.TargetStage))
        {
            throw DomainException.Validation("A target pipeline stage is required.");
        }

        var normalized = command with
        {
            TargetStage = command.TargetStage.Trim(),
            CloseReason = command.CloseReason?.Trim(),
            CompetitorName = command.CompetitorName?.Trim(),
            Notes = command.Notes?.Trim(),
        };

        return await store.TransitionStageAsync(tenantId, actorId, requestId, opportunityId, normalized, ct);
    }

    public async Task<ConvertedLeadResultDto> ConvertLeadAsync(
        Guid tenantId,
        Guid actorId,
        string requestId,
        Guid leadId,
        ConvertLeadCommand command,
        CancellationToken ct)
    {
        var normalized = command with
        {
            CustomerType = string.IsNullOrWhiteSpace(command.CustomerType) ? "Business" : command.CustomerType.Trim(),
            CustomerName = command.CustomerName?.Trim(),
            OpportunityName = command.OpportunityName?.Trim(),
            Currency = !string.IsNullOrWhiteSpace(command.Currency) ? command.Currency.Trim().ToUpperInvariant() : "USD",
        };

        return await store.ConvertLeadAtomicAsync(tenantId, actorId, requestId, leadId, normalized, ct);
    }
}
