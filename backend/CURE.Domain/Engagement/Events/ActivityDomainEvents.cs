namespace CURE.Domain.Engagement.Events;

using CURE.Domain.Shared;

/// <summary>
/// Raised when a rep logs any new activity against a customer, contact, or opportunity.
/// </summary>
public sealed record ActivityLoggedDomainEvent(
    Guid TenantId,
    DateTimeOffset OccurredAt,
    Guid ActivityId,
    ActivityType Type,
    string Subject,
    Guid? LinkedCustomerId,
    Guid? LinkedOpportunityId,
    Guid OwnerId,
    Guid ActorId) : DomainEvent(TenantId, OccurredAt)
{
    public override string AggregateType => "Activity";
    public override Guid   AggregateId   => ActivityId;
    public override string EventType     => "ActivityLogged";
    public override Guid?  CustomerId    => LinkedCustomerId;
}

/// <summary>
/// Raised when a rep marks an activity complete, capturing the outcome recorded.
/// Drives the opportunity health score recalculation and customer 360 timeline.
/// </summary>
public sealed record ActivityCompletedDomainEvent(
    Guid TenantId,
    DateTimeOffset OccurredAt,
    Guid ActivityId,
    ActivityType Type,
    string Subject,
    string? Outcome,
    Guid? LinkedCustomerId,
    Guid? LinkedOpportunityId,
    Guid ActorId) : DomainEvent(TenantId, OccurredAt)
{
    public override string AggregateType => "Activity";
    public override Guid   AggregateId   => ActivityId;
    public override string EventType     => "ActivityCompleted";
    public override Guid?  CustomerId    => LinkedCustomerId;
}

/// <summary>
/// Raised when an activity is cancelled before completion.
/// </summary>
public sealed record ActivityCancelledDomainEvent(
    Guid TenantId,
    DateTimeOffset OccurredAt,
    Guid ActivityId,
    ActivityType Type,
    string Subject,
    string? Reason,
    Guid? LinkedCustomerId,
    Guid ActorId) : DomainEvent(TenantId, OccurredAt)
{
    public override string AggregateType => "Activity";
    public override Guid   AggregateId   => ActivityId;
    public override string EventType     => "ActivityCancelled";
    public override Guid?  CustomerId    => LinkedCustomerId;
}
