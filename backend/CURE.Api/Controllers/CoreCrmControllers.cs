using CURE.Application.CoreCrm;
using CURE.Application.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CURE.Api.Controllers;

public abstract class CoreController(ICoreCrmService service, IRequestContextAccessor context) : ControllerBase
{
    protected Guid Tenant => context.Current.TenantId;
    protected Guid Actor => context.Current.UserId;
    protected string RequestId => context.Current.RequestId;
    protected static object Page<T>(PageResult<T> page) => new { data = page.Data, meta = new { page = page.Page, pageSize = page.PageSize, total = page.Total } };
    protected int Version(int? body) => body ?? (int.TryParse(Request.Headers.IfMatch.FirstOrDefault()?.Trim('"'), out var version) ? version : throw new BadHttpRequestException("Expected version is required."));
    protected async Task<IActionResult> DeleteResource(string resource, Guid id, CancellationToken ct) { await service.DeleteAsync(Tenant, Actor, RequestId, resource, id, ct); return NoContent(); }
}

[ApiController, Authorize, Route("api/v1/organizations")]
public sealed class OrganizationsController(ICoreCrmService s, IRequestContextAccessor c) : CoreController(s, c)
{
    [HttpGet] public async Task<IActionResult> List(string? search, int page = 1, int pageSize = 25, CancellationToken ct = default) => Ok(Page(await s.ListOrganizationsAsync(Tenant, new PageQuery(search, page, pageSize), ct)));
    [HttpGet("{id:guid}")] public async Task<IActionResult> Get(Guid id, CancellationToken ct) => Ok(new { data = await s.GetOrganizationAsync(Tenant, id, ct) });
    [HttpPost] public async Task<IActionResult> Create(OrganizationCommand x, CancellationToken ct) { var v = await s.CreateOrganizationAsync(Tenant, Actor, RequestId, x, ct); return CreatedAtAction(nameof(Get), new { id = v.Id }, new { data = v }); }
    [HttpPatch("{id:guid}")] public async Task<IActionResult> Update(Guid id, OrganizationCommand x, CancellationToken ct) => Ok(new { data = await s.UpdateOrganizationAsync(Tenant, Actor, RequestId, id, x with { ExpectedVersion = Version(x.ExpectedVersion) }, ct) });
    [HttpDelete("{id:guid}")] public Task<IActionResult> Delete(Guid id, CancellationToken ct) => DeleteResource("organizations", id, ct);
}

[ApiController, Authorize, Route("api/v1/contacts")]
public sealed class ContactsController(ICoreCrmService s, IRequestContextAccessor c) : CoreController(s, c)
{
    [HttpGet] public async Task<IActionResult> List(string? search, int page = 1, int pageSize = 25, CancellationToken ct = default) => Ok(Page(await s.ListContactsAsync(Tenant, new PageQuery(search, page, pageSize), ct)));
    [HttpGet("{id:guid}")] public async Task<IActionResult> Get(Guid id, CancellationToken ct) => Ok(new { data = await s.GetContactAsync(Tenant, id, ct) });
    [HttpPost] public async Task<IActionResult> Create(ContactCommand x, CancellationToken ct) { var v = await s.CreateContactAsync(Tenant, Actor, RequestId, x, ct); return CreatedAtAction(nameof(Get), new { id = v.Id }, new { data = v }); }
    [HttpPatch("{id:guid}")] public async Task<IActionResult> Update(Guid id, ContactCommand x, CancellationToken ct) => Ok(new { data = await s.UpdateContactAsync(Tenant, Actor, RequestId, id, x with { ExpectedVersion = Version(x.ExpectedVersion) }, ct) });
    [HttpDelete("{id:guid}")] public Task<IActionResult> Delete(Guid id, CancellationToken ct) => DeleteResource("contacts", id, ct);
}

[ApiController, Authorize, Route("api/v1/leads")]
public sealed class LeadsController(ICoreCrmService s, IRequestContextAccessor c) : CoreController(s, c)
{
    [HttpGet] public async Task<IActionResult> List(string? search, int page = 1, int pageSize = 25, CancellationToken ct = default) => Ok(Page(await s.ListLeadsAsync(Tenant, new PageQuery(search, page, pageSize), ct)));
    [HttpGet("{id:guid}")] public async Task<IActionResult> Get(Guid id, CancellationToken ct) => Ok(new { data = await s.GetLeadAsync(Tenant, id, ct) });
    [HttpPost] public async Task<IActionResult> Create(LeadCommand x, CancellationToken ct) { var v = await s.CreateLeadAsync(Tenant, Actor, RequestId, x, ct); return CreatedAtAction(nameof(Get), new { id = v.Id }, new { data = v }); }
    [HttpPatch("{id:guid}")] public async Task<IActionResult> Update(Guid id, LeadCommand x, CancellationToken ct) => Ok(new { data = await s.UpdateLeadAsync(Tenant, Actor, RequestId, id, x with { ExpectedVersion = Version(x.ExpectedVersion) }, ct) });
    [HttpDelete("{id:guid}")] public Task<IActionResult> Delete(Guid id, CancellationToken ct) => DeleteResource("leads", id, ct);
}

[ApiController, Authorize, Route("api/v1/opportunities")]
public sealed class OpportunitiesController(ICoreCrmService s, IRequestContextAccessor c) : CoreController(s, c)
{
    [HttpGet] public async Task<IActionResult> List(string? search, int page = 1, int pageSize = 25, CancellationToken ct = default) => Ok(Page(await s.ListOpportunitiesAsync(Tenant, new PageQuery(search, page, pageSize), ct)));
    [HttpGet("{id:guid}")] public async Task<IActionResult> Get(Guid id, CancellationToken ct) => Ok(new { data = await s.GetOpportunityAsync(Tenant, id, ct) });
    [HttpPost] public async Task<IActionResult> Create(OpportunityCommand x, CancellationToken ct) { var v = await s.CreateOpportunityAsync(Tenant, Actor, RequestId, x, ct); return CreatedAtAction(nameof(Get), new { id = v.Id }, new { data = v }); }
    [HttpPatch("{id:guid}")] public async Task<IActionResult> Update(Guid id, OpportunityCommand x, CancellationToken ct) => Ok(new { data = await s.UpdateOpportunityAsync(Tenant, Actor, RequestId, id, x with { ExpectedVersion = Version(x.ExpectedVersion) }, ct) });
    [HttpDelete("{id:guid}")] public Task<IActionResult> Delete(Guid id, CancellationToken ct) => DeleteResource("opportunities", id, ct);
}

[ApiController, Authorize, Route("api/v1/activities")]
public sealed class ActivitiesController(ICoreCrmService s, IRequestContextAccessor c) : CoreController(s, c)
{
    [HttpGet] public async Task<IActionResult> List(string? search, int page = 1, int pageSize = 25, CancellationToken ct = default) => Ok(Page(await s.ListActivitiesAsync(Tenant, new PageQuery(search, page, pageSize), ct)));
    [HttpGet("{id:guid}")] public async Task<IActionResult> Get(Guid id, CancellationToken ct) => Ok(new { data = await s.GetActivityAsync(Tenant, id, ct) });
    [HttpPost] public async Task<IActionResult> Create(ActivityCommand x, CancellationToken ct) { var v = await s.CreateActivityAsync(Tenant, Actor, RequestId, x, ct); return CreatedAtAction(nameof(Get), new { id = v.Id }, new { data = v }); }
    [HttpPatch("{id:guid}")] public async Task<IActionResult> Update(Guid id, ActivityCommand x, CancellationToken ct) => Ok(new { data = await s.UpdateActivityAsync(Tenant, Actor, RequestId, id, x with { ExpectedVersion = Version(x.ExpectedVersion) }, ct) });
    [HttpDelete("{id:guid}")] public Task<IActionResult> Delete(Guid id, CancellationToken ct) => DeleteResource("activities", id, ct);
}

[ApiController, Authorize, Route("api/v1/cases")]
public sealed class CasesController(ICoreCrmService s, IRequestContextAccessor c) : CoreController(s, c)
{
    [HttpGet] public async Task<IActionResult> List(string? search, int page = 1, int pageSize = 25, CancellationToken ct = default) => Ok(Page(await s.ListCasesAsync(Tenant, new PageQuery(search, page, pageSize), ct)));
    [HttpGet("{id:guid}")] public async Task<IActionResult> Get(Guid id, CancellationToken ct) => Ok(new { data = await s.GetCaseAsync(Tenant, id, ct) });
    [HttpPost] public async Task<IActionResult> Create(CaseCommand x, CancellationToken ct) { var v = await s.CreateCaseAsync(Tenant, Actor, RequestId, x, ct); return CreatedAtAction(nameof(Get), new { id = v.Id }, new { data = v }); }
    [HttpPatch("{id:guid}")] public async Task<IActionResult> Update(Guid id, CaseCommand x, CancellationToken ct) => Ok(new { data = await s.UpdateCaseAsync(Tenant, Actor, RequestId, id, x with { ExpectedVersion = Version(x.ExpectedVersion) }, ct) });
    [HttpDelete("{id:guid}")] public Task<IActionResult> Delete(Guid id, CancellationToken ct) => DeleteResource("cases", id, ct);
}

[ApiController, Authorize, Route("api/v1/saved-views")]
public sealed class SavedViewsController(ICoreCrmService s, IRequestContextAccessor c) : CoreController(s, c)
{
    [HttpGet] public async Task<IActionResult> List(string? search, int page = 1, int pageSize = 25, CancellationToken ct = default) => Ok(Page(await s.ListSavedViewsAsync(Tenant, new PageQuery(search, page, pageSize), ct)));
    [HttpGet("{id:guid}")] public async Task<IActionResult> Get(Guid id, CancellationToken ct) => Ok(new { data = await s.GetSavedViewAsync(Tenant, id, ct) });
    [HttpPost] public async Task<IActionResult> Create(SavedViewCommand x, CancellationToken ct) { var v = await s.CreateSavedViewAsync(Tenant, Actor, RequestId, x, ct); return CreatedAtAction(nameof(Get), new { id = v.Id }, new { data = v }); }
    [HttpPatch("{id:guid}")] public async Task<IActionResult> Update(Guid id, SavedViewCommand x, CancellationToken ct) => Ok(new { data = await s.UpdateSavedViewAsync(Tenant, Actor, RequestId, id, x with { ExpectedVersion = Version(x.ExpectedVersion) }, ct) });
    [HttpDelete("{id:guid}")] public Task<IActionResult> Delete(Guid id, CancellationToken ct) => DeleteResource("saved-views", id, ct);
}
