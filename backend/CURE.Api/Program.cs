using System.Security.Claims;
using System.Text;
using CURE.Application;
using CURE.Application.Shared;
using CURE.Infrastructure;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddCureApplication();
builder.Services.AddCureInfrastructure(builder.Configuration);
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<IRequestContextAccessor, HttpRequestContextAccessor>();
builder.Services.AddControllers(options => options.Filters.Add<CURE.Api.Controllers.ApiExceptionFilter>());
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddHealthChecks();
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(options =>
{
    var key = builder.Configuration["Authentication:SigningKey"] ?? "development-only-change-this-signing-key-please";
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)),
        ValidateIssuer = false,
        ValidateAudience = false,
        ValidateLifetime = true,
    };
});
builder.Services.AddAuthorization();

var app = builder.Build();
app.UseMiddleware<RequestContextMiddleware>();
app.UseAuthentication();
app.UseAuthorization();
app.MapHealthChecks("/health/live");
app.MapHealthChecks("/health/ready");
app.MapGet("/", () => Results.Ok(new { service = "CURE.Api", status = "running" }));
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
app.MapControllers();
app.Run();

public sealed class HttpRequestContextAccessor(IHttpContextAccessor accessor) : IRequestContextAccessor
{
    public RequestContext Current
    {
        get
        {
            var httpContext = accessor.HttpContext ?? throw new InvalidOperationException("No HTTP request context is available.");
            if (!Guid.TryParse(httpContext.User.FindFirstValue("tenant_id"), out var tenantId) || !Guid.TryParse(httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
                throw new InvalidOperationException("Authenticated tenant context is missing.");
            return new RequestContext(tenantId, userId, httpContext.TraceIdentifier);
        }
    }
}

public sealed class RequestContextMiddleware(RequestDelegate next, IWebHostEnvironment environment)
{
    public async Task InvokeAsync(HttpContext context)
    {
        if (environment.IsDevelopment() && context.User.Identity?.IsAuthenticated != true && Guid.TryParse(context.Request.Headers["X-CURE-Tenant-Id"], out var tenantId) && Guid.TryParse(context.Request.Headers["X-CURE-User-Id"], out var userId))
        {
            var identity = new ClaimsIdentity("DevelopmentHeader");
            identity.AddClaim(new Claim(ClaimTypes.NameIdentifier, userId.ToString()));
            identity.AddClaim(new Claim("tenant_id", tenantId.ToString()));
            identity.AddClaim(new Claim(ClaimTypes.Role, "Administrator"));
            context.User = new ClaimsPrincipal(identity);
        }
        await next(context);
    }
}
