namespace CURE.Application.Engagement;

using CURE.Domain.Shared;

/// <summary>
/// Application-layer orchestrator for activity and engagement operations.
///
/// Validation here is intentionally limited to input normalization and
/// cross-field rules that require no domain model load. All business-rule
/// enforcement (outcome required, terminal state guards, tenant isolation)
/// lives in <c>Activity</c> aggregate or the store transaction.
/// </summary>
public sealed class ActivityService(IActivityStore store) : IActivityService
{
    public async Task<ActivityDto> LogActivityAsync(
        Guid tenantId,
        Guid actorId,
        string requestId,
        LogActivityCommand command,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(command.Subject))
        {
            throw DomainException.Validation("Activity subject is required.");
        }

        var normalized = command with
        {
            Subject = command.Subject.Trim(),
            Notes   = command.Notes?.Trim(),
        };

        return await store.LogActivityAsync(tenantId, actorId, requestId, normalized, ct);
    }

    public async Task<ActivityDto> CompleteActivityAsync(
        Guid tenantId,
        Guid actorId,
        string requestId,
        Guid activityId,
        CompleteActivityCommand command,
        CancellationToken ct)
    {
        var normalized = command with
        {
            Outcome      = command.Outcome?.Trim(),
            ClosingNotes = command.ClosingNotes?.Trim(),
        };

        return await store.CompleteActivityAsync(tenantId, actorId, requestId, activityId, normalized, ct);
    }

    public Task<ActivityDto> CancelActivityAsync(
        Guid tenantId,
        Guid actorId,
        Guid activityId,
        CancelActivityCommand command,
        CancellationToken ct)
        => store.CancelActivityAsync(tenantId, actorId, activityId, command, ct);

    public Task<CustomerTimelineDto> GetCustomerTimelineAsync(
        Guid tenantId,
        Guid customerId,
        int limit,
        DateTimeOffset? before,
        CancellationToken ct)
    {
        limit = Math.Clamp(limit, 1, 100);
        return store.GetCustomerTimelineAsync(tenantId, customerId, limit, before, ct);
    }
}
