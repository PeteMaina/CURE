namespace CURE.Application.CoreCrm;

public sealed record PageQuery(string? Search, int Page, int PageSize);
public sealed record PageResult<T>(IReadOnlyList<T> Data, int Page, int PageSize, int Total);

public sealed record OrganizationDto(Guid Id, string Name, string? Industry, string? RegistrationNumber, DateTimeOffset UpdatedAt, int Version);
public sealed record OrganizationCommand(string Name, string? Industry, string? RegistrationNumber, int? ExpectedVersion = null);

public sealed record ContactDto(Guid Id, Guid? OrganizationId, string FirstName, string LastName, string? Email, string? Phone, string? Role, DateTimeOffset UpdatedAt, int Version);
public sealed record ContactCommand(Guid? OrganizationId, string FirstName, string LastName, string? Email, string? Phone, string? Role, int? ExpectedVersion = null);

public sealed record LeadDto(Guid Id, string LeadNumber, string FirstName, string LastName, string? CompanyName, string? Email, string Status, decimal? EstimatedValue, Guid? OwnerId, DateTimeOffset UpdatedAt, int Version);
public sealed record LeadCommand(string FirstName, string LastName, string? CompanyName, string? Email, string? Phone, string Source, decimal? EstimatedValue, string Status = "NEW", Guid? OwnerId = null, int? ExpectedVersion = null);

public sealed record OpportunityDto(Guid Id, string OpportunityNumber, string Name, Guid? CustomerId, string Stage, decimal Amount, string Currency, int Probability, DateOnly? ExpectedCloseDate, Guid? OwnerId, DateTimeOffset UpdatedAt, int Version);
public sealed record OpportunityCommand(string Name, Guid? CustomerId, string Stage, decimal Amount, string Currency, int Probability, DateOnly? ExpectedCloseDate, Guid? OwnerId, int? ExpectedVersion = null);

public sealed record ActivityDto(Guid Id, string Type, string Subject, string? Description, Guid? CustomerId, Guid? OwnerId, DateTimeOffset? ScheduledAt, string Status, DateTimeOffset UpdatedAt, int Version);
public sealed record ActivityCommand(string Type, string Subject, string? Description, Guid? CustomerId, Guid? OwnerId, DateTimeOffset? ScheduledAt, string Status = "PLANNED", int? ExpectedVersion = null);

public sealed record CaseDto(Guid Id, string CaseNumber, Guid? CustomerId, string Subject, string? Description, string Priority, string Status, Guid? OwnerId, DateTimeOffset UpdatedAt, int Version);
public sealed record CaseCommand(Guid? CustomerId, string Subject, string? Description, string Priority = "NORMAL", string Status = "OPEN", Guid? OwnerId = null, int? ExpectedVersion = null);

public sealed record SavedViewDto(Guid Id, string Name, string ResourceType, string ConfigurationJson, bool Shared, DateTimeOffset UpdatedAt, int Version);
public sealed record SavedViewCommand(string Name, string ResourceType, string ConfigurationJson, bool Shared = false, int? ExpectedVersion = null);

public interface ICoreCrmService
{
    Task<PageResult<OrganizationDto>> ListOrganizationsAsync(Guid tenantId, PageQuery query, CancellationToken ct);
    Task<OrganizationDto> GetOrganizationAsync(Guid tenantId, Guid id, CancellationToken ct);
    Task<OrganizationDto> CreateOrganizationAsync(Guid tenantId, Guid actorId, string requestId, OrganizationCommand command, CancellationToken ct);
    Task<OrganizationDto> UpdateOrganizationAsync(Guid tenantId, Guid actorId, string requestId, Guid id, OrganizationCommand command, CancellationToken ct);
    Task<PageResult<ContactDto>> ListContactsAsync(Guid tenantId, PageQuery query, CancellationToken ct);
    Task<ContactDto> GetContactAsync(Guid tenantId, Guid id, CancellationToken ct);
    Task<ContactDto> CreateContactAsync(Guid tenantId, Guid actorId, string requestId, ContactCommand command, CancellationToken ct);
    Task<ContactDto> UpdateContactAsync(Guid tenantId, Guid actorId, string requestId, Guid id, ContactCommand command, CancellationToken ct);
    Task<PageResult<LeadDto>> ListLeadsAsync(Guid tenantId, PageQuery query, CancellationToken ct);
    Task<LeadDto> GetLeadAsync(Guid tenantId, Guid id, CancellationToken ct);
    Task<LeadDto> CreateLeadAsync(Guid tenantId, Guid actorId, string requestId, LeadCommand command, CancellationToken ct);
    Task<LeadDto> UpdateLeadAsync(Guid tenantId, Guid actorId, string requestId, Guid id, LeadCommand command, CancellationToken ct);
    Task<PageResult<OpportunityDto>> ListOpportunitiesAsync(Guid tenantId, PageQuery query, CancellationToken ct);
    Task<OpportunityDto> GetOpportunityAsync(Guid tenantId, Guid id, CancellationToken ct);
    Task<OpportunityDto> CreateOpportunityAsync(Guid tenantId, Guid actorId, string requestId, OpportunityCommand command, CancellationToken ct);
    Task<OpportunityDto> UpdateOpportunityAsync(Guid tenantId, Guid actorId, string requestId, Guid id, OpportunityCommand command, CancellationToken ct);
    Task<PageResult<ActivityDto>> ListActivitiesAsync(Guid tenantId, PageQuery query, CancellationToken ct);
    Task<ActivityDto> GetActivityAsync(Guid tenantId, Guid id, CancellationToken ct);
    Task<ActivityDto> CreateActivityAsync(Guid tenantId, Guid actorId, string requestId, ActivityCommand command, CancellationToken ct);
    Task<ActivityDto> UpdateActivityAsync(Guid tenantId, Guid actorId, string requestId, Guid id, ActivityCommand command, CancellationToken ct);
    Task<PageResult<CaseDto>> ListCasesAsync(Guid tenantId, PageQuery query, CancellationToken ct);
    Task<CaseDto> GetCaseAsync(Guid tenantId, Guid id, CancellationToken ct);
    Task<CaseDto> CreateCaseAsync(Guid tenantId, Guid actorId, string requestId, CaseCommand command, CancellationToken ct);
    Task<CaseDto> UpdateCaseAsync(Guid tenantId, Guid actorId, string requestId, Guid id, CaseCommand command, CancellationToken ct);
    Task<PageResult<SavedViewDto>> ListSavedViewsAsync(Guid tenantId, PageQuery query, CancellationToken ct);
    Task<SavedViewDto> GetSavedViewAsync(Guid tenantId, Guid id, CancellationToken ct);
    Task<SavedViewDto> CreateSavedViewAsync(Guid tenantId, Guid actorId, string requestId, SavedViewCommand command, CancellationToken ct);
    Task<SavedViewDto> UpdateSavedViewAsync(Guid tenantId, Guid actorId, string requestId, Guid id, SavedViewCommand command, CancellationToken ct);
    Task DeleteAsync(Guid tenantId, Guid actorId, string requestId, string resource, Guid id, CancellationToken ct);
}