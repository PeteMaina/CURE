using CURE.Domain.Shared;

namespace CURE.Application.CoreCrm;

public interface ICoreCrmStore
{
    Task<PageResult<OrganizationDto>> ListOrganizationsAsync(Guid tenantId, PageQuery query, CancellationToken ct); Task<OrganizationDto?> GetOrganizationAsync(Guid tenantId, Guid id, CancellationToken ct); Task<OrganizationDto> CreateOrganizationAsync(Guid tenantId, OrganizationCommand command, CancellationToken ct); Task<OrganizationDto?> UpdateOrganizationAsync(Guid tenantId, Guid id, OrganizationCommand command, CancellationToken ct);
    Task<PageResult<ContactDto>> ListContactsAsync(Guid tenantId, PageQuery query, CancellationToken ct); Task<ContactDto?> GetContactAsync(Guid tenantId, Guid id, CancellationToken ct); Task<ContactDto> CreateContactAsync(Guid tenantId, ContactCommand command, CancellationToken ct); Task<ContactDto?> UpdateContactAsync(Guid tenantId, Guid id, ContactCommand command, CancellationToken ct);
    Task<PageResult<LeadDto>> ListLeadsAsync(Guid tenantId, PageQuery query, CancellationToken ct); Task<LeadDto?> GetLeadAsync(Guid tenantId, Guid id, CancellationToken ct); Task<LeadDto> CreateLeadAsync(Guid tenantId, LeadCommand command, CancellationToken ct); Task<LeadDto?> UpdateLeadAsync(Guid tenantId, Guid id, LeadCommand command, CancellationToken ct);
    Task<PageResult<OpportunityDto>> ListOpportunitiesAsync(Guid tenantId, PageQuery query, CancellationToken ct); Task<OpportunityDto?> GetOpportunityAsync(Guid tenantId, Guid id, CancellationToken ct); Task<OpportunityDto> CreateOpportunityAsync(Guid tenantId, OpportunityCommand command, CancellationToken ct); Task<OpportunityDto?> UpdateOpportunityAsync(Guid tenantId, Guid id, OpportunityCommand command, CancellationToken ct);
    Task<PageResult<ActivityDto>> ListActivitiesAsync(Guid tenantId, PageQuery query, CancellationToken ct); Task<ActivityDto?> GetActivityAsync(Guid tenantId, Guid id, CancellationToken ct); Task<ActivityDto> CreateActivityAsync(Guid tenantId, ActivityCommand command, CancellationToken ct); Task<ActivityDto?> UpdateActivityAsync(Guid tenantId, Guid id, ActivityCommand command, CancellationToken ct);
    Task<PageResult<CaseDto>> ListCasesAsync(Guid tenantId, PageQuery query, CancellationToken ct); Task<CaseDto?> GetCaseAsync(Guid tenantId, Guid id, CancellationToken ct); Task<CaseDto> CreateCaseAsync(Guid tenantId, CaseCommand command, CancellationToken ct); Task<CaseDto?> UpdateCaseAsync(Guid tenantId, Guid id, CaseCommand command, CancellationToken ct);
    Task<PageResult<SavedViewDto>> ListSavedViewsAsync(Guid tenantId, PageQuery query, CancellationToken ct); Task<SavedViewDto?> GetSavedViewAsync(Guid tenantId, Guid id, CancellationToken ct); Task<SavedViewDto> CreateSavedViewAsync(Guid tenantId, SavedViewCommand command, CancellationToken ct); Task<SavedViewDto?> UpdateSavedViewAsync(Guid tenantId, Guid id, SavedViewCommand command, CancellationToken ct);
    Task WriteAuditAsync(Guid tenantId, Guid actorId, string requestId, string entityType, Guid entityId, object? before, object? after, CancellationToken ct);
    Task<bool> DeleteAsync(Guid tenantId, string resource, Guid id, CancellationToken ct);
}

public sealed class CoreCrmService(ICoreCrmStore store) : ICoreCrmService
{
    public Task<PageResult<OrganizationDto>> ListOrganizationsAsync(Guid t, PageQuery q, CancellationToken c) => store.ListOrganizationsAsync(t, Normalize(q), c);
    public Task<PageResult<ContactDto>> ListContactsAsync(Guid t, PageQuery q, CancellationToken c) => store.ListContactsAsync(t, Normalize(q), c);
    public Task<PageResult<LeadDto>> ListLeadsAsync(Guid t, PageQuery q, CancellationToken c) => store.ListLeadsAsync(t, Normalize(q), c);
    public Task<PageResult<OpportunityDto>> ListOpportunitiesAsync(Guid t, PageQuery q, CancellationToken c) => store.ListOpportunitiesAsync(t, Normalize(q), c);
    public Task<PageResult<ActivityDto>> ListActivitiesAsync(Guid t, PageQuery q, CancellationToken c) => store.ListActivitiesAsync(t, Normalize(q), c);
    public Task<PageResult<CaseDto>> ListCasesAsync(Guid t, PageQuery q, CancellationToken c) => store.ListCasesAsync(t, Normalize(q), c);
    public Task<PageResult<SavedViewDto>> ListSavedViewsAsync(Guid t, PageQuery q, CancellationToken c) => store.ListSavedViewsAsync(t, Normalize(q), c);
    public async Task<OrganizationDto> GetOrganizationAsync(Guid t, Guid id, CancellationToken c) => await store.GetOrganizationAsync(t, id, c) ?? throw DomainException.NotFound("Organization", id);
    public async Task<ContactDto> GetContactAsync(Guid t, Guid id, CancellationToken c) => await store.GetContactAsync(t, id, c) ?? throw DomainException.NotFound("Contact", id);
    public async Task<LeadDto> GetLeadAsync(Guid t, Guid id, CancellationToken c) => await store.GetLeadAsync(t, id, c) ?? throw DomainException.NotFound("Lead", id);
    public async Task<OpportunityDto> GetOpportunityAsync(Guid t, Guid id, CancellationToken c) => await store.GetOpportunityAsync(t, id, c) ?? throw DomainException.NotFound("Opportunity", id);
    public async Task<ActivityDto> GetActivityAsync(Guid t, Guid id, CancellationToken c) => await store.GetActivityAsync(t, id, c) ?? throw DomainException.NotFound("Activity", id);
    public async Task<CaseDto> GetCaseAsync(Guid t, Guid id, CancellationToken c) => await store.GetCaseAsync(t, id, c) ?? throw DomainException.NotFound("Case", id);
    public async Task<SavedViewDto> GetSavedViewAsync(Guid t, Guid id, CancellationToken c) => await store.GetSavedViewAsync(t, id, c) ?? throw DomainException.NotFound("Saved view", id);
    public async Task<OrganizationDto> CreateOrganizationAsync(Guid t, Guid a, string r, OrganizationCommand x, CancellationToken c) => await Create(t, a, r, x, store.CreateOrganizationAsync, "Organization", c);
    public async Task<ContactDto> CreateContactAsync(Guid t, Guid a, string r, ContactCommand x, CancellationToken c) => await Create(t, a, r, x, store.CreateContactAsync, "Contact", c);
    public async Task<LeadDto> CreateLeadAsync(Guid t, Guid a, string r, LeadCommand x, CancellationToken c) => await Create(t, a, r, x, store.CreateLeadAsync, "Lead", c);
    public async Task<OpportunityDto> CreateOpportunityAsync(Guid t, Guid a, string r, OpportunityCommand x, CancellationToken c) => await Create(t, a, r, x, store.CreateOpportunityAsync, "Opportunity", c);
    public async Task<ActivityDto> CreateActivityAsync(Guid t, Guid a, string r, ActivityCommand x, CancellationToken c) => await Create(t, a, r, x, store.CreateActivityAsync, "Activity", c);
    public async Task<CaseDto> CreateCaseAsync(Guid t, Guid a, string r, CaseCommand x, CancellationToken c) => await Create(t, a, r, x, store.CreateCaseAsync, "Case", c);
    public async Task<SavedViewDto> CreateSavedViewAsync(Guid t, Guid a, string r, SavedViewCommand x, CancellationToken c) => await Create(t, a, r, x, store.CreateSavedViewAsync, "SavedView", c);
    public async Task<OrganizationDto> UpdateOrganizationAsync(Guid t, Guid a, string r, Guid id, OrganizationCommand x, CancellationToken c) => await Update(t, a, r, id, x, store.GetOrganizationAsync, store.UpdateOrganizationAsync, "Organization", c);
    public async Task<ContactDto> UpdateContactAsync(Guid t, Guid a, string r, Guid id, ContactCommand x, CancellationToken c) => await Update(t, a, r, id, x, store.GetContactAsync, store.UpdateContactAsync, "Contact", c);
    public async Task<LeadDto> UpdateLeadAsync(Guid t, Guid a, string r, Guid id, LeadCommand x, CancellationToken c) => await Update(t, a, r, id, x, store.GetLeadAsync, store.UpdateLeadAsync, "Lead", c);
    public async Task<OpportunityDto> UpdateOpportunityAsync(Guid t, Guid a, string r, Guid id, OpportunityCommand x, CancellationToken c) => await Update(t, a, r, id, x, store.GetOpportunityAsync, store.UpdateOpportunityAsync, "Opportunity", c);
    public async Task<ActivityDto> UpdateActivityAsync(Guid t, Guid a, string r, Guid id, ActivityCommand x, CancellationToken c) => await Update(t, a, r, id, x, store.GetActivityAsync, store.UpdateActivityAsync, "Activity", c);
    public async Task<CaseDto> UpdateCaseAsync(Guid t, Guid a, string r, Guid id, CaseCommand x, CancellationToken c) => await Update(t, a, r, id, x, store.GetCaseAsync, store.UpdateCaseAsync, "Case", c);
    public async Task<SavedViewDto> UpdateSavedViewAsync(Guid t, Guid a, string r, Guid id, SavedViewCommand x, CancellationToken c) => await Update(t, a, r, id, x, store.GetSavedViewAsync, store.UpdateSavedViewAsync, "SavedView", c);
    public async Task DeleteAsync(Guid t, Guid a, string r, string resource, Guid id, CancellationToken c) { if (!await store.DeleteAsync(t, resource, id, c)) throw DomainException.NotFound(resource, id); await store.WriteAuditAsync(t, a, r, resource.ToUpperInvariant() + "_DELETED", id, null, null, c); }
    private static PageQuery Normalize(PageQuery q) => new(q.Search, Math.Max(1, q.Page), Math.Clamp(q.PageSize, 1, 100));
    private async Task<T> Create<T, TCommand>(Guid t, Guid a, string r, TCommand x, Func<Guid, TCommand, CancellationToken, Task<T>> create, string entity, CancellationToken c) { var value = await create(t, x, c); await store.WriteAuditAsync(t, a, r, entity + "_CREATED", Guid.Parse(value!.GetType().GetProperty("Id")!.GetValue(value)!.ToString()!), null, value, c); return value; }
    private async Task<T> Update<T, TCommand>(Guid t, Guid a, string r, Guid id, TCommand x, Func<Guid, Guid, CancellationToken, Task<T?>> get, Func<Guid, Guid, TCommand, CancellationToken, Task<T?>> update, string entity, CancellationToken c) { var before = await get(t, id, c) ?? throw DomainException.NotFound(entity, id); var value = await update(t, id, x, c) ?? throw new DomainException(ErrorCodes.ConcurrentModification, "This record changed while you were editing it."); await store.WriteAuditAsync(t, a, r, entity.ToUpperInvariant() + "_UPDATED", id, before, value, c); return value; }
}