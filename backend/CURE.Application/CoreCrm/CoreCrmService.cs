using CURE.Domain.Shared;

namespace CURE.Application.CoreCrm;

public interface ICoreCrmStore
{
    Task<PageResult<OrganizationDto>> ListOrganizationsAsync(Guid tenantId, PageQuery query, CancellationToken ct); Task<OrganizationDto?> GetOrganizationAsync(Guid tenantId, Guid id, CancellationToken ct); Task<OrganizationDto> CreateOrganizationAsync(Guid tenantId, Guid actorId, string requestId, OrganizationCommand command, CancellationToken ct); Task<OrganizationDto?> UpdateOrganizationAsync(Guid tenantId, Guid actorId, string requestId, Guid id, OrganizationCommand command, CancellationToken ct);
    Task<PageResult<ContactDto>> ListContactsAsync(Guid tenantId, PageQuery query, CancellationToken ct); Task<ContactDto?> GetContactAsync(Guid tenantId, Guid id, CancellationToken ct); Task<ContactDto> CreateContactAsync(Guid tenantId, Guid actorId, string requestId, ContactCommand command, CancellationToken ct); Task<ContactDto?> UpdateContactAsync(Guid tenantId, Guid actorId, string requestId, Guid id, ContactCommand command, CancellationToken ct);
    Task<PageResult<LeadDto>> ListLeadsAsync(Guid tenantId, PageQuery query, CancellationToken ct); Task<LeadDto?> GetLeadAsync(Guid tenantId, Guid id, CancellationToken ct); Task<LeadDto> CreateLeadAsync(Guid tenantId, Guid actorId, string requestId, LeadCommand command, CancellationToken ct); Task<LeadDto?> UpdateLeadAsync(Guid tenantId, Guid actorId, string requestId, Guid id, LeadCommand command, CancellationToken ct);
    Task<PageResult<OpportunityDto>> ListOpportunitiesAsync(Guid tenantId, PageQuery query, CancellationToken ct); Task<OpportunityDto?> GetOpportunityAsync(Guid tenantId, Guid id, CancellationToken ct); Task<OpportunityDto> CreateOpportunityAsync(Guid tenantId, Guid actorId, string requestId, OpportunityCommand command, CancellationToken ct); Task<OpportunityDto?> UpdateOpportunityAsync(Guid tenantId, Guid actorId, string requestId, Guid id, OpportunityCommand command, CancellationToken ct);
    Task<PageResult<ActivityDto>> ListActivitiesAsync(Guid tenantId, PageQuery query, CancellationToken ct); Task<ActivityDto?> GetActivityAsync(Guid tenantId, Guid id, CancellationToken ct); Task<ActivityDto> CreateActivityAsync(Guid tenantId, Guid actorId, string requestId, ActivityCommand command, CancellationToken ct); Task<ActivityDto?> UpdateActivityAsync(Guid tenantId, Guid actorId, string requestId, Guid id, ActivityCommand command, CancellationToken ct);
    Task<PageResult<CaseDto>> ListCasesAsync(Guid tenantId, PageQuery query, CancellationToken ct); Task<CaseDto?> GetCaseAsync(Guid tenantId, Guid id, CancellationToken ct); Task<CaseDto> CreateCaseAsync(Guid tenantId, Guid actorId, string requestId, CaseCommand command, CancellationToken ct); Task<CaseDto?> UpdateCaseAsync(Guid tenantId, Guid actorId, string requestId, Guid id, CaseCommand command, CancellationToken ct);
    Task<PageResult<SavedViewDto>> ListSavedViewsAsync(Guid tenantId, PageQuery query, CancellationToken ct); Task<SavedViewDto?> GetSavedViewAsync(Guid tenantId, Guid id, CancellationToken ct); Task<SavedViewDto> CreateSavedViewAsync(Guid tenantId, Guid actorId, string requestId, SavedViewCommand command, CancellationToken ct); Task<SavedViewDto?> UpdateSavedViewAsync(Guid tenantId, Guid actorId, string requestId, Guid id, SavedViewCommand command, CancellationToken ct);
    Task<bool> DeleteAsync(Guid tenantId, Guid actorId, string requestId, string resource, Guid id, CancellationToken ct);
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
    public async Task<OrganizationDto> CreateOrganizationAsync(Guid t, Guid a, string r, OrganizationCommand x, CancellationToken c) => await store.CreateOrganizationAsync(t, a, r, x, c);
    public async Task<ContactDto> CreateContactAsync(Guid t, Guid a, string r, ContactCommand x, CancellationToken c) => await store.CreateContactAsync(t, a, r, x, c);
    public async Task<LeadDto> CreateLeadAsync(Guid t, Guid a, string r, LeadCommand x, CancellationToken c) => await store.CreateLeadAsync(t, a, r, x, c);
    public async Task<OpportunityDto> CreateOpportunityAsync(Guid t, Guid a, string r, OpportunityCommand x, CancellationToken c) => await store.CreateOpportunityAsync(t, a, r, x, c);
    public async Task<ActivityDto> CreateActivityAsync(Guid t, Guid a, string r, ActivityCommand x, CancellationToken c) => await store.CreateActivityAsync(t, a, r, x, c);
    public async Task<CaseDto> CreateCaseAsync(Guid t, Guid a, string r, CaseCommand x, CancellationToken c) => await store.CreateCaseAsync(t, a, r, x, c);
    public async Task<SavedViewDto> CreateSavedViewAsync(Guid t, Guid a, string r, SavedViewCommand x, CancellationToken c) => await store.CreateSavedViewAsync(t, a, r, x, c);
    public async Task<OrganizationDto> UpdateOrganizationAsync(Guid t, Guid a, string r, Guid id, OrganizationCommand x, CancellationToken c) => await Update(id, () => store.GetOrganizationAsync(t, id, c), () => store.UpdateOrganizationAsync(t, a, r, id, x, c), "Organization");
    public async Task<ContactDto> UpdateContactAsync(Guid t, Guid a, string r, Guid id, ContactCommand x, CancellationToken c) => await Update(id, () => store.GetContactAsync(t, id, c), () => store.UpdateContactAsync(t, a, r, id, x, c), "Contact");
    public async Task<LeadDto> UpdateLeadAsync(Guid t, Guid a, string r, Guid id, LeadCommand x, CancellationToken c) => await Update(id, () => store.GetLeadAsync(t, id, c), () => store.UpdateLeadAsync(t, a, r, id, x, c), "Lead");
    public async Task<OpportunityDto> UpdateOpportunityAsync(Guid t, Guid a, string r, Guid id, OpportunityCommand x, CancellationToken c) => await Update(id, () => store.GetOpportunityAsync(t, id, c), () => store.UpdateOpportunityAsync(t, a, r, id, x, c), "Opportunity");
    public async Task<ActivityDto> UpdateActivityAsync(Guid t, Guid a, string r, Guid id, ActivityCommand x, CancellationToken c) => await Update(id, () => store.GetActivityAsync(t, id, c), () => store.UpdateActivityAsync(t, a, r, id, x, c), "Activity");
    public async Task<CaseDto> UpdateCaseAsync(Guid t, Guid a, string r, Guid id, CaseCommand x, CancellationToken c) => await Update(id, () => store.GetCaseAsync(t, id, c), () => store.UpdateCaseAsync(t, a, r, id, x, c), "Case");
    public async Task<SavedViewDto> UpdateSavedViewAsync(Guid t, Guid a, string r, Guid id, SavedViewCommand x, CancellationToken c) => await Update(id, () => store.GetSavedViewAsync(t, id, c), () => store.UpdateSavedViewAsync(t, a, r, id, x, c), "Saved view");
    public async Task DeleteAsync(Guid t, Guid a, string r, string resource, Guid id, CancellationToken c) { if (!await store.DeleteAsync(t, a, r, resource, id, c)) throw DomainException.NotFound(resource, id); }
    private static PageQuery Normalize(PageQuery q) => new(q.Search, Math.Max(1, q.Page), Math.Clamp(q.PageSize, 1, 100));
    private static async Task<T> Update<T>(Guid id, Func<Task<T?>> get, Func<Task<T?>> update, string entity) where T : class
    {
        _ = await get() ?? throw DomainException.NotFound(entity, id);
        return await update() ?? throw new DomainException(ErrorCodes.ConcurrentModification, "This record changed while you were editing it.");
    }
}