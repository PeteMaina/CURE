using CURE.Application.Customers;
using CURE.Application.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CURE.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/customers")]
public sealed class CustomersController(ICustomerService customers, IRequestContextAccessor requestContext) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] string? search, [FromQuery] int page = 1, [FromQuery] int pageSize = 25, CancellationToken cancellationToken = default)
    {
        var result = await customers.ListAsync(requestContext.Current.TenantId, new CustomerListQuery(search, page, pageSize), cancellationToken);
        return Ok(new { data = result.Data, meta = new { page = result.Page, pageSize = result.PageSize, total = result.Total } });
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken cancellationToken)
        => Ok(new { data = await customers.GetAsync(requestContext.Current.TenantId, id, cancellationToken) });

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateCustomerCommand command, CancellationToken cancellationToken)
    {
        var current = requestContext.Current;
        var created = await customers.CreateAsync(current.TenantId, current.UserId, current.RequestId, command, cancellationToken);
        return CreatedAtAction(nameof(Get), new { id = created.Id }, new { data = created });
    }

    [HttpPatch("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateCustomerRequest request, CancellationToken cancellationToken)
    {
        var current = requestContext.Current;
        var expectedVersion = request.ExpectedVersion ?? ParseVersionHeader();
        var updated = await customers.UpdateAsync(current.TenantId, current.UserId, current.RequestId, id, new UpdateCustomerCommand(request.Name, request.Type, request.Industry, request.Email, request.Phone, request.OwnerId, expectedVersion), cancellationToken);
        return Ok(new { data = updated });
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var current = requestContext.Current;
        await customers.DeleteAsync(current.TenantId, current.UserId, current.RequestId, id, cancellationToken);
        return NoContent();
    }

    private int ParseVersionHeader()
        => int.TryParse(Request.Headers.IfMatch.FirstOrDefault()?.Trim('"'), out var version) ? version : throw new BadHttpRequestException("Expected version is required.");
}

public sealed record UpdateCustomerRequest(string Name, string Type, string? Industry, string? Email, string? Phone, Guid? OwnerId, int? ExpectedVersion);
