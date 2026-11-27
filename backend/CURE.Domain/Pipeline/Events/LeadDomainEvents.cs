namespace CURE.Domain.Pipeline.Events;

using CURE.Domain.Shared;

public sealed record LeadCreatedDomainEvent(
    Guid TenantId,
    DateTimeOffset OccurredAt,
    Guid LeadId,
    string LeadNumber,
    string FullName,
    string CompanyName,
    string? LeadSource,
    Guid ActorId) : DomainEvent(TenantId, OccurredAt)
{
    public override string AggregateType => "Lead";
    public override Guid AggregateId => LeadId;
    public override string EventType => "LeadCreated";
}

public sealed record LeadStatusChangedDomainEvent(
    Guid TenantId,
    DateTimeOffset OccurredAt,
    Guid LeadId,
    string LeadNumber,
    LeadStatus OldStatus,
    LeadStatus NewStatus,
    Guid ActorId) : DomainEvent(TenantId, OccurredAt)
{
    public override string AggregateType => "Lead";
    public override Guid AggregateId => LeadId;
    public override string EventType => "LeadStatusChanged";
}

public sealed record LeadDisqualifiedDomainEvent(
    Guid TenantId,
    DateTimeOffset OccurredAt,
    Guid LeadId,
    string LeadNumber,
    string Reason,
    Guid ActorId) : DomainEvent(TenantId, OccurredAt)
{
    public override string AggregateType => "Lead";
    public override Guid AggregateId => LeadId;
    public override string EventType => "LeadDisqualified";
}

public sealed record LeadConvertedDomainEvent(
    Guid TenantId,
    DateTimeOffset OccurredAt,
    Guid LeadId,
    string LeadNumber,
    Guid ConvertedAccountId,
    Guid? ContactId,
    Guid? OpportunityId,
    Guid ActorId) : DomainEvent(TenantId, OccurredAt)
{
    public override string AggregateType => "Lead";
    public override Guid AggregateId => LeadId;
    public override string EventType => "LeadConverted";
    public override Guid? CustomerId => ConvertedAccountId;
}
