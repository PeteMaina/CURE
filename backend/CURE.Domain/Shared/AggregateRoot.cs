namespace CURE.Domain.Shared;

/// <summary>
/// Base class for every tenant-owned aggregate root.
///
/// Provides the record shape, identity, tenant,
/// created/updated attribution and a concurrency version — plus domain-event
/// collection.
///
///  The aggregate never increments its own
/// version, which keeps a single writer of that value and makes lost updates
/// impossible to introduce by accident.

public abstract class AggregateRoot : IEmitsDomainEvents
{
    private readonly List<DomainEvent> _domainEvents = new();

    protected AggregateRoot(
        Guid id,
        Guid tenantId,
        DateTimeOffset createdAt,
        Guid createdBy,
        DateTimeOffset updatedAt,
        Guid updatedBy,
        int version)
    {
        Id = id;
        TenantId = tenantId;
        CreatedAt = createdAt;
        CreatedBy = createdBy;
        UpdatedAt = updatedAt;
        UpdatedBy = updatedBy;
        Version = version;
    }

    public Guid Id { get; }

    public Guid TenantId { get; }

    public DateTimeOffset CreatedAt { get; }

    public Guid CreatedBy { get; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public Guid UpdatedBy { get; private set; }

    public int Version { get; private set; }

    public IReadOnlyList<DomainEvent> DomainEvents => _domainEvents;

    /// <summary>Records who changed the aggregate and when. Does not touch the version.</summary>
    protected void Touch(Guid actorId, DateTimeOffset at)
    {
        UpdatedBy = actorId;
        UpdatedAt = at;
    }

    protected void Raise(DomainEvent domainEvent) => _domainEvents.Add(domainEvent);

    public void ClearDomainEvents() => _domainEvents.Clear();

    /// <summary>
    /// Called by the repository after a successful write, with the version the
    /// database actually assigned. Lets a caller continue using the instance
    /// (and issue a second update) without a reload.
    /// </summary>
    public void MarkPersisted(int persistedVersion) => Version = persistedVersion;

    /// <summary>
    /// Guards against operating on an aggregate belonging to a different tenant.
    
    public void AssertBelongsToTenant(Guid tenantId)
    {
        if (TenantId != tenantId)
        {
            throw new DomainException(
                ErrorCodes.CrossTenantAccessDenied,
                "The requested record is not available.");
        }
    }
}
