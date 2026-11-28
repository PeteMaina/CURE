namespace CURE.Domain.Engagement;

using CURE.Domain.Engagement.Events;
using CURE.Domain.Shared;

/// <summary>
/// Activity aggregate root — the atomic unit of customer engagement.
///
/// An activity represents one real-world touch-point between a rep and a
/// customer or prospect: a phone call logged, a meeting held, a note pinned.
/// Every mutation raises a domain event so the outbox can fan-out to the
/// customer timeline, the opportunity history, and any downstream
/// notification surface without coupling the aggregate to infrastructure.
///
/// Concurrency: same optimistic-lock pattern as Opportunity — the store
/// compares the in-memory Version against the database row and raises
/// <see cref="ConcurrencyException"/> on conflict, making double-completion
/// physically impossible without a distributed lock.
/// </summary>
public sealed class Activity : AggregateRoot
{
    private Activity(
        Guid id,
        Guid tenantId,
        ActivityType type,
        ActivityStatus status,
        string subject,
        string? notes,
        DateTimeOffset? scheduledAt,
        DateTimeOffset? completedAt,
        int? durationMinutes,
        string? outcome,
        Guid? linkedCustomerId,
        Guid? linkedContactId,
        Guid? linkedOpportunityId,
        Guid ownerId,
        DateTimeOffset createdAt,
        Guid createdBy,
        DateTimeOffset updatedAt,
        Guid updatedBy,
        int version)
        : base(id, tenantId, createdAt, createdBy, updatedAt, updatedBy, version)
    {
        Type                 = type;
        Status               = status;
        Subject              = subject;
        Notes                = notes;
        ScheduledAt          = scheduledAt;
        CompletedAt          = completedAt;
        DurationMinutes      = durationMinutes;
        Outcome              = outcome;
        LinkedCustomerId     = linkedCustomerId;
        LinkedContactId      = linkedContactId;
        LinkedOpportunityId  = linkedOpportunityId;
        OwnerId              = ownerId;
    }

    // ── Identity & context ────────────────────────────────────────────────────
    public ActivityType    Type                { get; }
    public ActivityStatus  Status              { get; private set; }
    public string          Subject             { get; }
    public string?         Notes               { get; private set; }

    // ── Scheduling ────────────────────────────────────────────────────────────
    public DateTimeOffset? ScheduledAt         { get; }
    public DateTimeOffset? CompletedAt         { get; private set; }
    public int?            DurationMinutes     { get; private set; }

    // ── Outcome ───────────────────────────────────────────────────────────────
    public string?         Outcome             { get; private set; }

    // ── Context links — drives Customer 360 and opportunity timeline ──────────
    public Guid?           LinkedCustomerId    { get; }
    public Guid?           LinkedContactId     { get; }
    public Guid?           LinkedOpportunityId { get; }

    // ── Ownership ─────────────────────────────────────────────────────────────
    public Guid            OwnerId             { get; }

    // ── Factory ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Creates and returns a new <see cref="Activity"/> in <see cref="ActivityStatus.Planned"/>
    /// state, raising <see cref="ActivityLoggedDomainEvent"/> for the outbox.
    /// </summary>
    public static Activity Log(
        Guid tenantId,
        ActivityType type,
        string subject,
        string? notes,
        DateTimeOffset? scheduledAt,
        int? durationMinutes,
        Guid? linkedCustomerId,
        Guid? linkedContactId,
        Guid? linkedOpportunityId,
        Guid ownerId,
        Guid actorId,
        DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(subject))
        {
            throw DomainException.Validation("Activity subject is required.");
        }

        var id = Guid.NewGuid();
        var activity = new Activity(
            id: id,
            tenantId: tenantId,
            type: type,
            status: ActivityStatus.Planned,
            subject: subject.Trim(),
            notes: notes?.Trim(),
            scheduledAt: scheduledAt,
            completedAt: null,
            durationMinutes: durationMinutes ?? type.DefaultDurationMinutes() switch { 0 => null, var d => d },
            outcome: null,
            linkedCustomerId: linkedCustomerId,
            linkedContactId: linkedContactId,
            linkedOpportunityId: linkedOpportunityId,
            ownerId: ownerId,
            createdAt: now,
            createdBy: actorId,
            updatedAt: now,
            updatedBy: actorId,
            version: 0);

        activity.Raise(new ActivityLoggedDomainEvent(
            TenantId: tenantId,
            OccurredAt: now,
            ActivityId: id,
            Type: type,
            Subject: activity.Subject,
            LinkedCustomerId: linkedCustomerId,
            LinkedOpportunityId: linkedOpportunityId,
            OwnerId: ownerId,
            ActorId: actorId));

        return activity;
    }

    // ── Mutations ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Marks this activity complete, optionally recording an outcome string
    /// and actual call/meeting duration.
    /// </summary>
    /// <exception cref="DomainException">
    /// Raised when the activity is already in a terminal state, or when the
    /// activity type mandates an outcome and none is supplied.
    /// </exception>
    public void Complete(
        string? outcome,
        int? actualDurationMinutes,
        string? closingNotes,
        Guid actorId,
        DateTimeOffset now)
    {
        if (Status.IsTerminal())
        {
            throw new DomainException(
                ErrorCodes.ActivityInvalidStatusTransition,
                $"Cannot complete an activity that is already {Status.Label()}.");
        }

        if (Type.OutcomeRequired() && string.IsNullOrWhiteSpace(outcome))
        {
            throw new DomainException(
                ErrorCodes.ActivityOutcomeRequired,
                $"An outcome is required when completing a {Type.Label()} activity.");
        }

        Status          = ActivityStatus.Completed;
        Outcome         = outcome?.Trim();
        CompletedAt     = now;
        DurationMinutes = actualDurationMinutes ?? DurationMinutes;
        Notes           = closingNotes?.Trim() ?? Notes;

        Touch(actorId, now);

        Raise(new ActivityCompletedDomainEvent(
            TenantId: TenantId,
            OccurredAt: now,
            ActivityId: Id,
            Type: Type,
            Subject: Subject,
            Outcome: Outcome,
            LinkedCustomerId: LinkedCustomerId,
            LinkedOpportunityId: LinkedOpportunityId,
            ActorId: actorId));
    }

    /// <summary>
    /// Cancels the activity. Terminal activities may not be cancelled.
    /// </summary>
    public void Cancel(string? reason, Guid actorId, DateTimeOffset now)
    {
        if (Status.IsTerminal())
        {
            throw new DomainException(
                ErrorCodes.ActivityInvalidStatusTransition,
                $"Cannot cancel an activity that is already {Status.Label()}.");
        }

        Status = ActivityStatus.Cancelled;
        Notes  = reason?.Trim() ?? Notes;
        Touch(actorId, now);

        Raise(new ActivityCancelledDomainEvent(
            TenantId: TenantId,
            OccurredAt: now,
            ActivityId: Id,
            Type: Type,
            Subject: Subject,
            Reason: reason?.Trim(),
            LinkedCustomerId: LinkedCustomerId,
            ActorId: actorId));
    }

    // ── Reconstitution (store → aggregate) ────────────────────────────────────

    /// <summary>
    /// Rehydrates an activity from a database row without raising any events.
    /// Called exclusively by the persistence layer.
    /// </summary>
    public static Activity Reconstitute(
        Guid id,
        Guid tenantId,
        ActivityType type,
        ActivityStatus status,
        string subject,
        string? notes,
        DateTimeOffset? scheduledAt,
        DateTimeOffset? completedAt,
        int? durationMinutes,
        string? outcome,
        Guid? linkedCustomerId,
        Guid? linkedContactId,
        Guid? linkedOpportunityId,
        Guid ownerId,
        DateTimeOffset createdAt,
        Guid createdBy,
        DateTimeOffset updatedAt,
        Guid updatedBy,
        int version)
        => new(id, tenantId, type, status, subject, notes,
               scheduledAt, completedAt, durationMinutes, outcome,
               linkedCustomerId, linkedContactId, linkedOpportunityId,
               ownerId, createdAt, createdBy, updatedAt, updatedBy, version);
}
