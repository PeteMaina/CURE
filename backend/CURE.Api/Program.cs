using System.Security.Claims;
using System.Text;
using CURE.Application;
using CURE.Application.Shared;
using CURE.Infrastructure;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.IdentityModel.Tokens;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);
var isDevelopment = builder.Environment.IsDevelopment();
var authority = builder.Configuration["Authentication:Authority"];
var audience = builder.Configuration["Authentication:Audience"];
var signingKey = builder.Configuration["Authentication:SigningKey"];

if (!isDevelopment && (string.IsNullOrWhiteSpace(authority) || string.IsNullOrWhiteSpace(audience)))
{
    throw new InvalidOperationException(
        "Production authentication requires Authentication:Authority and Authentication:Audience.");
}

if (isDevelopment && string.IsNullOrWhiteSpace(signingKey))
{
    throw new InvalidOperationException(
        "Development authentication requires Authentication:SigningKey.");
}

builder.Services.AddCureApplication();
builder.Services.AddCureInfrastructure(builder.Configuration);
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<IRequestContextAccessor, HttpRequestContextAccessor>();
builder.Services.AddControllers(options => options.Filters.Add<CURE.Api.Controllers.ApiExceptionFilter>());
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services
    .AddHealthChecks()
    .AddCheck<PostgresReadinessCheck>("postgres", tags: ["ready"]);
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
    {
        var tenant = context.User.FindFirstValue("tenant_id");
        var user = context.User.FindFirstValue("user_id")
            ?? context.User.FindFirstValue(ClaimTypes.NameIdentifier);
        var key = !string.IsNullOrWhiteSpace(tenant) && !string.IsNullOrWhiteSpace(user)
            ? $"user:{tenant}:{user}"
            : $"anonymous:{context.Connection.RemoteIpAddress}";
        return RateLimitPartition.GetFixedWindowLimiter(
            key,
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = string.IsNullOrWhiteSpace(user) ? 20 : 120,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
                AutoReplenishment = true,
            });
    });
});
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(options =>
{
    options.Authority = authority;
    options.Audience = audience;
    options.RequireHttpsMetadata = !isDevelopment;
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = isDevelopment
            ? new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey!))
            : null,
        ValidateIssuer = !isDevelopment,
        ValidIssuer = authority,
        ValidateAudience = !isDevelopment,
        ValidAudience = audience,
        ValidateLifetime = true,
        NameClaimType = "name",
        RoleClaimType = "role",
    };
    options.Events = new JwtBearerEvents
    {
        OnTokenValidated = context =>
        {
            var principal = context.Principal;
            var tenantId = principal?.FindFirstValue("tenant_id");
            var userId = principal?.FindFirstValue("user_id")
                ?? principal?.FindFirstValue(ClaimTypes.NameIdentifier)
                ?? principal?.FindFirstValue("sub");

            if (!Guid.TryParse(tenantId, out _) || !Guid.TryParse(userId, out _))
            {
                context.Fail("The access token is missing a valid CURE tenant or user identity.");
            }
            else if (!principal!.HasClaim(claim => claim.Type == ClaimTypes.NameIdentifier))
            {
                ((ClaimsIdentity)principal.Identity!).AddClaim(
                    new Claim(ClaimTypes.NameIdentifier, userId!));
            }

            return Task.CompletedTask;
        },
    };
});
builder.Services.AddAuthorization(options =>
{
    options.FallbackPolicy = options.DefaultPolicy;
    options.AddPolicy("CustomerWrite", policy =>
        policy.RequireRole(CureRoles.Administrator, CureRoles.SalesManager, CureRoles.SalesRepresentative, CureRoles.SupportManager));
    options.AddPolicy("ContactWrite", policy =>
        policy.RequireRole(CureRoles.Administrator, CureRoles.SalesManager, CureRoles.SalesRepresentative, CureRoles.SupportManager, CureRoles.SupportAgent));
    options.AddPolicy("SalesWrite", policy =>
        policy.RequireRole(CureRoles.Administrator, CureRoles.SalesManager, CureRoles.SalesRepresentative));
    options.AddPolicy("ActivityWrite", policy =>
        policy.RequireRole(CureRoles.Administrator, CureRoles.SalesManager, CureRoles.SalesRepresentative, CureRoles.SupportManager, CureRoles.SupportAgent));
    options.AddPolicy("CaseWrite", policy =>
        policy.RequireRole(CureRoles.Administrator, CureRoles.SupportManager, CureRoles.SupportAgent));
});

var app = builder.Build();
app.UseMiddleware<RequestContextMiddleware>();
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();
app.Use(async (context, next) =>
{
    context.Response.Headers["X-Content-Type-Options"] = "nosniff";
    context.Response.Headers["X-Frame-Options"] = "DENY";
    context.Response.Headers["Referrer-Policy"] = "no-referrer";
    await next();
});
app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = _ => false,
}).AllowAnonymous();
app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready"),
}).AllowAnonymous();
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
            if (!Guid.TryParse(httpContext.User.FindFirstValue("tenant_id"), out var tenantId)
                || !Guid.TryParse(httpContext.User.FindFirstValue("user_id")
                    ?? httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
                throw new InvalidOperationException("Authenticated tenant context is missing.");
            return new RequestContext(tenantId, userId, httpContext.TraceIdentifier);
        }
    }
}

public sealed class RequestContextMiddleware(
    RequestDelegate next,
    IWebHostEnvironment environment,
    IConfiguration configuration)
{
    public async Task InvokeAsync(HttpContext context)
    {
        if (environment.IsDevelopment()
            && configuration.GetValue("Authentication:AllowDevelopmentHeaders", true)
            && context.User.Identity?.IsAuthenticated != true
            && Guid.TryParse(context.Request.Headers["X-CURE-Tenant-Id"], out var tenantId)
            && Guid.TryParse(context.Request.Headers["X-CURE-User-Id"], out var userId))
        {
            var identity = new ClaimsIdentity("DevelopmentHeader");
            identity.AddClaim(new Claim(ClaimTypes.NameIdentifier, userId.ToString()));
            identity.AddClaim(new Claim("user_id", userId.ToString()));
            identity.AddClaim(new Claim("tenant_id", tenantId.ToString()));
            identity.AddClaim(new Claim(ClaimTypes.Role, "Administrator"));
            context.User = new ClaimsPrincipal(identity);
        }
        await next(context);
    }
}

public sealed class PostgresReadinessCheck(
    CURE.Infrastructure.Persistence.PostgresConnectionFactory connectionFactory) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await using var connection = await connectionFactory.OpenAsync(cancellationToken);
            await using var command = connection.CreateCommand();
            command.CommandText = """
                WITH RECURSIVE role_memberships(member_oid, role_oid) AS (
                    SELECT member, roleid FROM pg_auth_members
                    UNION
                    SELECT role_memberships.member_oid, memberships.roleid
                    FROM role_memberships
                    JOIN pg_auth_members memberships
                      ON memberships.member = role_memberships.role_oid
                )
                SELECT runtime_role.rolsuper
                    OR runtime_role.rolbypassrls
                    OR runtime_role.rolcreaterole
                    OR runtime_role.rolcreatedb
                    OR runtime_role.rolreplication
                    OR has_schema_privilege(runtime_role.oid, 'public', 'CREATE')
                    OR EXISTS (
                        SELECT 1
                        FROM role_memberships membership
                        WHERE membership.member_oid = runtime_role.oid
                    )
                    OR EXISTS (
                        SELECT 1
                        FROM pg_database database_info
                        WHERE database_info.datname = current_database()
                          AND (
                              database_info.datdba = runtime_role.oid
                              OR EXISTS (
                                  SELECT 1
                                  FROM role_memberships membership
                                  WHERE membership.member_oid = runtime_role.oid
                                    AND membership.role_oid = database_info.datdba
                              )
                          )
                    )
                    OR EXISTS (
                        SELECT 1
                        FROM role_memberships membership
                        JOIN pg_roles inherited_role ON inherited_role.oid = membership.role_oid
                        WHERE membership.member_oid = runtime_role.oid
                          AND (inherited_role.rolsuper
                            OR inherited_role.rolbypassrls
                            OR inherited_role.rolcreaterole
                            OR inherited_role.rolcreatedb
                            OR inherited_role.rolreplication)
                    )
                    OR EXISTS (
                        SELECT 1
                        FROM pg_class table_info
                        JOIN pg_namespace schema_info ON schema_info.oid = table_info.relnamespace
                        WHERE schema_info.nspname = 'public'
                          AND table_info.relkind IN ('r', 'p')
                          AND (
                              table_info.relowner = runtime_role.oid
                              OR EXISTS (
                                  SELECT 1
                                  FROM role_memberships membership
                                  WHERE membership.member_oid = runtime_role.oid
                                    AND membership.role_oid = table_info.relowner
                              )
                          )
                    )
                    FROM pg_roles runtime_role
                    WHERE runtime_role.rolname = current_user;
                """;
            var unsafeRole = (bool?)await command.ExecuteScalarAsync(cancellationToken);
            if (unsafeRole is null || unsafeRole.Value)
            {
                return HealthCheckResult.Unhealthy(
                    "The application database role has privileges or ownership membership that are unsafe for runtime access.");
            }

            command.CommandText = """
                WITH required_tables(table_name, policy_name) AS (
                    VALUES
                        ('tenants', 'tenant_isolation_tenants'),
                        ('users', 'tenant_isolation_users'),
                        ('customers', 'tenant_isolation_customers'),
                        ('audit_events', 'tenant_isolation_audit'),
                        ('organizations', 'tenant_isolation_organizations'),
                        ('contacts', 'tenant_isolation_contacts'),
                        ('leads', 'tenant_isolation_leads'),
                        ('opportunities', 'tenant_isolation_opportunities'),
                        ('activities', 'tenant_isolation_activities'),
                        ('cases', 'tenant_isolation_cases'),
                        ('saved_views', 'tenant_isolation_saved_views'),
                        ('customer_contact_links', 'tenant_isolation_customer_contact_links'),
                        ('notification_states', 'tenant_isolation_notification_states')
                )
                SELECT count(*)::int
                FROM required_tables expected_table
                LEFT JOIN pg_namespace schema_info ON schema_info.nspname = 'public'
                LEFT JOIN pg_class table_info ON table_info.relnamespace = schema_info.oid
                    AND table_info.relname = expected_table.table_name
                WHERE table_info.oid IS NULL
                   OR NOT table_info.relrowsecurity
                   OR NOT table_info.relforcerowsecurity
                   OR NOT EXISTS (
                       SELECT 1
                       FROM pg_policies policy_info
                       WHERE policy_info.schemaname = 'public'
                         AND policy_info.tablename = expected_table.table_name
                         AND policy_info.policyname = expected_table.policy_name
                   );
                """;
            var invalidTables = (int)(await command.ExecuteScalarAsync(cancellationToken) ?? 0);
            if (invalidTables != 0)
            {
                return HealthCheckResult.Unhealthy(
                    "One or more tenant tables are missing or have incomplete forced row-level security.");
            }

            command.CommandText = """
                SELECT COALESCE(max(version), 0) >= 2
                FROM cure_schema_migrations;
                """;
            var migrationsApplied = (bool?)await command.ExecuteScalarAsync(cancellationToken);
            if (migrationsApplied != true)
            {
                return HealthCheckResult.Unhealthy(
                    "The required database migrations have not been applied.");
            }

            return HealthCheckResult.Healthy("PostgreSQL is reachable.");
        }
        catch (Npgsql.NpgsqlException)
        {
            return HealthCheckResult.Unhealthy("PostgreSQL is unavailable.");
        }
    }
}

public static class CureRoles
{
    public const string Administrator = "Administrator";
    public const string SalesManager = "Sales Manager";
    public const string SalesRepresentative = "Sales Representative";
    public const string SupportManager = "Support Manager";
    public const string SupportAgent = "Support Agent";
}
