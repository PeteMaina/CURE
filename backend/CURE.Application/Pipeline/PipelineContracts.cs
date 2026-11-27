namespace CURE.Application.Pipeline;

using CURE.Application.CoreCrm;

public sealed record StageAggregationDto(
    string Stage,
    int DealCount,
    decimal TotalAmount,
    decimal WeightedAmount,
    double AverageDaysInStage);

public sealed record PipelineSummaryDto(
    IReadOnlyList<StageAggregationDto> Stages,
    decimal TotalPipelineAmount,
    decimal TotalWeightedAmount,
    int TotalOpenDeals,
    double WinRatePercentage);

public sealed record OpportunityStageHistoryDto(
    Guid Id,
    Guid OpportunityId,
    string? FromStage,
    string ToStage,
    decimal Amount,
    string Currency,
    int Probability,
    decimal WeightedAmount,
    int? DaysInPriorStage,
    DateTimeOffset ChangedAt,
    Guid ChangedBy,
    string? CloseReason,
    string? Notes);

public sealed record TransitionStageCommand(
    string TargetStage,
    string? CloseReason = null,
    string? CompetitorName = null,
    string? Notes = null,
    int? ExpectedVersion = null);

public sealed record ConvertLeadCommand(
    string CustomerType = "Business",
    string? CustomerName = null,
    bool CreateOpportunity = false,
    string? OpportunityName = null,
    decimal? OpportunityAmount = null,
    string? Currency = null,
    DateOnly? ExpectedCloseDate = null);

public sealed record ConvertedLeadResultDto(
    Guid LeadId,
    Guid CustomerId,
    Guid ContactId,
    Guid? OpportunityId,
    DateTimeOffset ConvertedAt);

public interface IPipelineStore
{
    Task<PipelineSummaryDto> GetPipelineSummaryAsync(Guid tenantId, CancellationToken ct);
    Task<IReadOnlyList<OpportunityStageHistoryDto>> GetStageHistoryAsync(Guid tenantId, Guid opportunityId, CancellationToken ct);
    Task<OpportunityDto> TransitionStageAsync(Guid tenantId, Guid actorId, string requestId, Guid opportunityId, TransitionStageCommand command, CancellationToken ct);
    Task<ConvertedLeadResultDto> ConvertLeadAtomicAsync(Guid tenantId, Guid actorId, string requestId, Guid leadId, ConvertLeadCommand command, CancellationToken ct);
}

public interface IPipelineService
{
    Task<PipelineSummaryDto> GetPipelineSummaryAsync(Guid tenantId, CancellationToken ct);
    Task<IReadOnlyList<OpportunityStageHistoryDto>> GetStageHistoryAsync(Guid tenantId, Guid opportunityId, CancellationToken ct);
    Task<OpportunityDto> TransitionStageAsync(Guid tenantId, Guid actorId, string requestId, Guid opportunityId, TransitionStageCommand command, CancellationToken ct);
    Task<ConvertedLeadResultDto> ConvertLeadAsync(Guid tenantId, Guid actorId, string requestId, Guid leadId, ConvertLeadCommand command, CancellationToken ct);
}
