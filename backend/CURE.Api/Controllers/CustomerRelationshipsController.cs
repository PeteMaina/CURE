using CURE.Application.Shared;
using CURE.Application.Workspace;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CURE.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/customers/{customerId:guid}")]
public sealed class CustomerRelationshipsController(
    IWorkspaceService workspace,
    IRequestContextAccessor requestContext) : ControllerBase
{
    [HttpGet("360")]
    public async Task<IActionResult> Get360(Guid customerId, CancellationToken ct)
    {
        var tenantId = requestContext.Current.TenantId;
        return Ok(new { data = await workspace.GetCustomer360Async(tenantId, customerId, ct) });
    }

    [Authorize(Policy = "ContactWrite")]
    [HttpPost("contacts")]
    public async Task<IActionResult> LinkContact(
        Guid customerId,
        [FromBody] LinkCustomerContactRequest request,
        CancellationToken ct)
    {
        var current = requestContext.Current;
        await workspace.LinkContactAsync(
            current.TenantId,
            current.UserId,
            current.RequestId,
            customerId,
            request.ContactId,
            ct);
        return NoContent();
    }

    [Authorize(Policy = "ContactWrite")]
    [HttpDelete("contacts/{contactId:guid}")]
    public async Task<IActionResult> UnlinkContact(Guid customerId, Guid contactId, CancellationToken ct)
    {
        var current = requestContext.Current;
        await workspace.UnlinkContactAsync(
            current.TenantId,
            current.UserId,
            current.RequestId,
            customerId,
            contactId,
            ct);
        return NoContent();
    }
}

public sealed record LinkCustomerContactRequest(Guid ContactId);
