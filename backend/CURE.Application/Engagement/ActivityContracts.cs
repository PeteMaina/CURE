namespace CURE.Application.Engagement;

using CURE.Application.CoreCrm;

// ── Contracts (DTOs + store interface) ────────────────────────────────────────

public sealed record LogActivityCommand(
    ActivityTypeDto Type,
    string Subject,
    string? Notes = null,
    DateTimeOffset? ScheduledAt = null,
    int? DurationMinutes = null,
    Guid? LinkedCustomerId = null,
    Guid? LinkedContactId = null,
    Guid? LinkedOpportunityId = null);

public sealed record CompleteActivityCommand(
    string? Outcome = null,
    int? ActualDurationMinutes = null,
    string? ClosingNotes = null,
    int? ExpectedVersion = null);

public sealed record CancelActivityCommand(
    string? Reason = null,
    int? ExpectedVersion = null);

public enum ActivityTypeDto
{
    Call            = 1,
    Email           = 2,
    Meeting         = 3,
    Note            = 4,
    Task            = 5,
    Demo            = 6,
    LinkedInMessage = 7,
    SmsMessage      = 8,
}

public sealed record ActivityDto(
    Guid Id,
    Guid TenantId,
    string Type,
    string Status,
    string Subject,
    string? Notes,
    DateTimeOffset? ScheduledAt,
    DateTimeOffset? CompletedAt,
    int? DurationMinutes,
    string? Outcome,
    Guid? LinkedCustomerId,
    Guid? LinkedContactId,
    Guid? LinkedOpportunityId,
    Guid OwnerId,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    int Version);

/// <summary>
/// A single entry in a customer's unified timeline, sourced from one of several
/// underlying tables (activities, opportunity stage changes, audit events) via
/// the <c>customer_timeline_view</c> database view.
/// </summary>
public sealed record TimelineEntryDto(
    Guid EntryId,
    string EntryKind,         // "Activity" | "OpportunityStageChange" | "AuditEvent"
    string EventType,
    string Summary,
    string? Detail,
    Guid? LinkedOpportunityId,
    Guid? LinkedActivityId,
    Guid ActorId,
    DateTimeOffset OccurredAt);

public sealed record CustomerTimelineDto(
    Guid CustomerId,
    IReadOnlyList<TimelineEntryDto> Entries,
    bool HasMore,
    DateTimeOffset? OldestEntryAt);

public interface IActivityStore
{
    Task<ActivityDto> LogActivityAsync(
        Guid tenantId,
        Guid actorId,
        string requestId,
        LogActivityCommand command,
        CancellationToken ct);

    Task<ActivityDto> CompleteActivityAsync(
        Guid tenantId,
        Guid actorId,
        string requestId,
        Guid activityId,
        CompleteActivityCommand command,
        CancellationToken ct);

    Task<ActivityDto> CancelActivityAsync(
        Guid tenantId,
        Guid actorId,
        Guid activityId,
        CancelActivityCommand command,
        CancellationToken ct);

    Task<CustomerTimelineDto> GetCustomerTimelineAsync(
        Guid tenantId,
        Guid customerId,
        int limit,
        DateTimeOffset? before,
        CancellationToken ct);
}

public interface IActivityService
{
    Task<ActivityDto> LogActivityAsync(
        Guid tenantId,
        Guid actorId,
        string requestId,
        LogActivityCommand command,
        CancellationToken ct);

    Task<ActivityDto> CompleteActivityAsync(
        Guid tenantId,
        Guid actorId,
        string requestId,
        Guid activityId,
        CompleteActivityCommand command,
        CancellationToken ct);

    Task<ActivityDto> CancelActivityAsync(
        Guid tenantId,
        Guid actorId,
        Guid activityId,
        CancelActivityCommand command,
        CancellationToken ct);

    Task<CustomerTimelineDto> GetCustomerTimelineAsync(
        Guid tenantId,
        Guid customerId,
        int limit,
        DateTimeOffset? before,
        CancellationToken ct);
}
