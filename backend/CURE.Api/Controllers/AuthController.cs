using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;

namespace CURE.Api.Controllers;

[ApiController]
[Route("api/v1/auth")]
public sealed class AuthController(IConfiguration configuration, IWebHostEnvironment environment) : ControllerBase
{
    [AllowAnonymous]
    [HttpPost("development-token")]
    public IActionResult DevelopmentToken([FromBody] DevelopmentLoginRequest request)
    {
        if (!environment.IsDevelopment()) return NotFound();
        var expectedPassword = configuration["Authentication:DevelopmentPassword"] ?? "cure-development-only";
        if (!string.Equals(request.Password, expectedPassword, StringComparison.Ordinal)) return Unauthorized(new { error = new { code = "INVALID_CREDENTIALS", message = "Invalid credentials." } });
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, "00000000-0000-0000-0000-000000000002"),
            new Claim("tenant_id", "00000000-0000-0000-0000-000000000001"),
            new Claim(ClaimTypes.Role, "Administrator"),
        };
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(configuration["Authentication:SigningKey"] ?? "development-only-change-this-signing-key-please"));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(claims: claims, expires: DateTime.UtcNow.AddHours(8), signingCredentials: credentials);
        return Ok(new { accessToken = new JwtSecurityTokenHandler().WriteToken(token), tokenType = "Bearer", expiresIn = 28800 });
    }
}

public sealed record DevelopmentLoginRequest(string Password);
