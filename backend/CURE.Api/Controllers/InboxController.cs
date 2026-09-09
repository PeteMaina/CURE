using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CURE.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/inbox")]
public sealed class InboxController : ControllerBase
{
    [HttpGet]
    public IActionResult List() => Ok(new
    {
        data = Array.Empty<object>(),
        meta = new { total = 0, generatedAt = DateTimeOffset.UtcNow },
    });
}
