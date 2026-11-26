using CURE.Application.Customers;

namespace CURE.Application.Workspace;

public sealed record WorkspaceCounts(
    int Customers,
    int Leads,
    int Opportunities,
    int OpenCases,
    int DueActivities);

public sealed record WorkspaceActivity(
    Guid Id,
    string Type,
    string Subject,
    string Status,
    DateTimeOffset? ScheduledAt,
    Guid? CustomerId,
    string? CustomerName);

public sealed record InboxItem(
    string EntityType,
    Guid EntityId,
    string Category,
    string Title,
    string? Description,
    Guid? CustomerId,
    string? CustomerName,
    string Status,
    string? Priority,
    DateTimeOffset? DueAt,
    DateTimeOffset UpdatedAt,
    bool IsRead);

public sealed record InboxPage(
    IReadOnlyList<InboxItem> Data,
    int Page,
    int PageSize,
    int Total,
    int DueActivityCount,
    int OpenCaseCount);

public sealed record HomeDashboard(
    string UserName,
    DateTimeOffset GeneratedAt,
    WorkspaceCounts Counts,
    IReadOnlyList<InboxItem> AttentionItems,
    IReadOnlyList<CustomerListItem> RecentCustomers,
    IReadOnlyList<WorkspaceActivity> RecentActivities);

public sealed record CustomerContactView(
    Guid Id,
    string FirstName,
    string LastName,
    string? Email,
    string? Phone,
    string? Role,
    DateTimeOffset LinkedAt);

public sealed record RelatedOpportunityView(
    Guid Id,
    string OpportunityNumber,
    string Name,
    string Stage,
    decimal Amount,
    string Currency,
    DateOnly? ExpectedCloseDate,
    DateTimeOffset UpdatedAt);

public sealed record RelatedCaseView(
    Guid Id,
    string CaseNumber,
    string Subject,
    string Priority,
    string Status,
    DateTimeOffset UpdatedAt);

public sealed record RelatedActivityView(
    Guid Id,
    string Type,
    string Subject,
    string? Description,
    string Status,
    DateTimeOffset? ScheduledAt,
    DateTimeOffset UpdatedAt);

public sealed record CustomerTimelineEntry(
    Guid Id,
    string EntityType,
    string Action,
    Guid EntityId,
    Guid ActorId,
    DateTimeOffset OccurredAt);

public sealed record Customer360Metrics(
    int Contacts,
    int Opportunities,
    int Cases,
    int OpenCases,
    int Activities);

public sealed record Customer360View(
    CustomerDetail Customer,
    Customer360Metrics Metrics,
    IReadOnlyList<CustomerContactView> Contacts,
    IReadOnlyList<RelatedOpportunityView> Opportunities,
    IReadOnlyList<RelatedCaseView> Cases,
    IReadOnlyList<RelatedActivityView> Activities,
    IReadOnlyList<CustomerTimelineEntry> Timeline);

public sealed record SearchHit(
    string EntityType,
    Guid Id,
    string Title,
    string? Detail,
    DateTimeOffset UpdatedAt);

public sealed record NotificationFeed(
    IReadOnlyList<InboxItem> Data,
    int UnreadCount);

public interface IWorkspaceStore
{
    Task<HomeDashboard> GetDashboardAsync(Guid tenantId, Guid userId, CancellationToken ct);
    Task<InboxPage> GetInboxAsync(Guid tenantId, Guid userId, string? category, int page, int pageSize, CancellationToken ct);
    Task<Customer360View?> GetCustomer360Async(Guid tenantId, Guid customerId, CancellationToken ct);
    Task<IReadOnlyList<SearchHit>> SearchAsync(Guid tenantId, string query, int limit, CancellationToken ct);
    Task<NotificationFeed> GetNotificationsAsync(Guid tenantId, Guid userId, int limit, CancellationToken ct);
    Task SetNotificationReadAsync(Guid tenantId, Guid userId, string entityType, Guid entityId, bool isRead, CancellationToken ct);
    Task LinkContactAsync(Guid tenantId, Guid actorId, string requestId, Guid customerId, Guid contactId, CancellationToken ct);
    Task UnlinkContactAsync(Guid tenantId, Guid actorId, string requestId, Guid customerId, Guid contactId, CancellationToken ct);
}

public interface IWorkspaceService
{
    Task<HomeDashboard> GetDashboardAsync(Guid tenantId, Guid userId, CancellationToken ct);
    Task<InboxPage> GetInboxAsync(Guid tenantId, Guid userId, string? category, int page, int pageSize, CancellationToken ct);
    Task<Customer360View> GetCustomer360Async(Guid tenantId, Guid customerId, CancellationToken ct);
    Task<IReadOnlyList<SearchHit>> SearchAsync(Guid tenantId, string query, int limit, CancellationToken ct);
    Task<NotificationFeed> GetNotificationsAsync(Guid tenantId, Guid userId, int limit, CancellationToken ct);
    Task SetNotificationReadAsync(Guid tenantId, Guid userId, string entityType, Guid entityId, bool isRead, CancellationToken ct);
    Task LinkContactAsync(Guid tenantId, Guid actorId, string requestId, Guid customerId, Guid contactId, CancellationToken ct);
    Task UnlinkContactAsync(Guid tenantId, Guid actorId, string requestId, Guid customerId, Guid contactId, CancellationToken ct);
}
