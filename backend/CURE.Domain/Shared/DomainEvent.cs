namespace CURE.Domain.Shared;

/// <summary>
/// A fact that has already happened, in the past tense 
public abstract record DomainEvent
{
    protected DomainEvent(Guid tenantId, DateTimeOffset occurredAt)
    {
        EventId = CureId.NewAt(occurredAt);
        TenantId = tenantId;
        OccurredAt = occurredAt;
    }

    public Guid EventId { get; }

    public Guid TenantId { get; }

    /// <summary>When the fact happened in the business world.</summary>
    public DateTimeOffset OccurredAt { get; }

    /// <summary>The aggregate this fact is about, for timeline and outbox routing.</summary>
    public abstract string AggregateType { get; }

    public abstract Guid AggregateId { get; }

    public abstract string EventType { get; }

    public virtual int EventVersion => 1;

    /// <summary>
    /// The customer this fact ultimately concerns, when one applies. Drives
    /// Customer 360 timeline placement without every consumer re-deriving it.
   
    public virtual Guid? CustomerId => null;

    /// <summary>Qualified name written to the outbox, e.g. <c>CustomerCreated.v1</c>.</summary>
    public string QualifiedName => $"{EventType}.v{EventVersion}";
}

/// <summary>
/// Implemented by aggregates that accumulate events during a transaction.

public interface IEmitsDomainEvents
{
    IReadOnlyList<DomainEvent> DomainEvents { get; }

    void ClearDomainEvents();
}
