namespace CURE.Domain.Pipeline.Events;

using CURE.Domain.Shared;

public sealed record OpportunityCreatedDomainEvent(
    Guid TenantId,
    DateTimeOffset OccurredAt,
    Guid OpportunityId,
    string OpportunityNumber,
    string Name,
    Guid? LinkedCustomerId,
    OpportunityStage Stage,
    Money Amount,
    Percentage Probability,
    Guid ActorId) : DomainEvent(TenantId, OccurredAt)
{
    public override string AggregateType => "Opportunity";
    public override Guid AggregateId => OpportunityId;
    public override string EventType => "OpportunityCreated";
    public override Guid? CustomerId => LinkedCustomerId;
}

public sealed record OpportunityStageChangedDomainEvent(
    Guid TenantId,
    DateTimeOffset OccurredAt,
    Guid OpportunityId,
    string OpportunityNumber,
    Guid? LinkedCustomerId,
    OpportunityStage OldStage,
    OpportunityStage NewStage,
    Money Amount,
    Percentage Probability,
    Guid ActorId) : DomainEvent(TenantId, OccurredAt)
{
    public override string AggregateType => "Opportunity";
    public override Guid AggregateId => OpportunityId;
    public override string EventType => "OpportunityStageChanged";
    public override Guid? CustomerId => LinkedCustomerId;
}

public sealed record OpportunityWonDomainEvent(
    Guid TenantId,
    DateTimeOffset OccurredAt,
    Guid OpportunityId,
    string OpportunityNumber,
    Guid CustomerAccountId,
    Money Amount,
    string WinReason,
    Guid ActorId) : DomainEvent(TenantId, OccurredAt)
{
    public override string AggregateType => "Opportunity";
    public override Guid AggregateId => OpportunityId;
    public override string EventType => "OpportunityWon";
    public override Guid? CustomerId => CustomerAccountId;
}

public sealed record OpportunityLostDomainEvent(
    Guid TenantId,
    DateTimeOffset OccurredAt,
    Guid OpportunityId,
    string OpportunityNumber,
    Guid? LinkedCustomerId,
    Money Amount,
    string LossReason,
    string? CompetitorName,
    Guid ActorId) : DomainEvent(TenantId, OccurredAt)
{
    public override string AggregateType => "Opportunity";
    public override Guid AggregateId => OpportunityId;
    public override string EventType => "OpportunityLost";
    public override Guid? CustomerId => LinkedCustomerId;
}

public sealed record OpportunityReopenedDomainEvent(
    Guid TenantId,
    DateTimeOffset OccurredAt,
    Guid OpportunityId,
    string OpportunityNumber,
    OpportunityStage PriorStage,
    OpportunityStage TargetStage,
    string Justification,
    Guid ActorId) : DomainEvent(TenantId, OccurredAt)
{
    public override string AggregateType => "Opportunity";
    public override Guid AggregateId => OpportunityId;
    public override string EventType => "OpportunityReopened";
}
