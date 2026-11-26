using CURE.Domain.Shared;

namespace CURE.Application.Workspace;

public sealed class WorkspaceService(IWorkspaceStore store) : IWorkspaceService
{
    private static readonly HashSet<string> NotificationEntityTypes =
        new(StringComparer.OrdinalIgnoreCase) { "activity", "case" };

    public Task<HomeDashboard> GetDashboardAsync(Guid tenantId, Guid userId, CancellationToken ct)
        => store.GetDashboardAsync(tenantId, userId, ct);

    public Task<InboxPage> GetInboxAsync(
        Guid tenantId,
        Guid userId,
        string? category,
        int page,
        int pageSize,
        CancellationToken ct)
    {
        var normalizedCategory = string.IsNullOrWhiteSpace(category)
            ? null
            : category.Trim().ToLowerInvariant();

        if (normalizedCategory is not null
            && normalizedCategory is not ("activity" or "case"))
        {
            throw DomainException.Validation("Choose an activity or case inbox filter.");
        }

        return store.GetInboxAsync(
            tenantId,
            userId,
            normalizedCategory,
            Math.Max(1, page),
            Math.Clamp(pageSize, 1, 100),
            ct);
    }

    public async Task<Customer360View> GetCustomer360Async(Guid tenantId, Guid customerId, CancellationToken ct)
        => await store.GetCustomer360Async(tenantId, customerId, ct)
           ?? throw DomainException.NotFound("Customer", customerId);

    public async Task<IReadOnlyList<SearchHit>> SearchAsync(
        Guid tenantId,
        string query,
        int limit,
        CancellationToken ct)
    {
        var normalized = query.Trim();
        if (normalized.Length < 2)
        {
            throw DomainException.Validation("Enter at least two characters to search.");
        }

        if (normalized.Length > 100)
        {
            throw DomainException.Validation("Search text cannot exceed 100 characters.");
        }

        return await store.SearchAsync(tenantId, normalized, Math.Clamp(limit, 1, 50), ct);
    }

    public Task<NotificationFeed> GetNotificationsAsync(Guid tenantId, Guid userId, int limit, CancellationToken ct)
        => store.GetNotificationsAsync(tenantId, userId, Math.Clamp(limit, 1, 50), ct);

    public Task SetNotificationReadAsync(
        Guid tenantId,
        Guid userId,
        string entityType,
        Guid entityId,
        bool isRead,
        CancellationToken ct)
    {
        if (!NotificationEntityTypes.Contains(entityType))
        {
            throw DomainException.Validation("This notification type is not supported.");
        }

        return store.SetNotificationReadAsync(tenantId, userId, entityType.ToLowerInvariant(), entityId, isRead, ct);
    }

    public Task LinkContactAsync(
        Guid tenantId,
        Guid actorId,
        string requestId,
        Guid customerId,
        Guid contactId,
        CancellationToken ct)
        => store.LinkContactAsync(tenantId, actorId, requestId, customerId, contactId, ct);

    public Task UnlinkContactAsync(
        Guid tenantId,
        Guid actorId,
        string requestId,
        Guid customerId,
        Guid contactId,
        CancellationToken ct)
        => store.UnlinkContactAsync(tenantId, actorId, requestId, customerId, contactId, ct);
}
