using System.Text.Json;
using CURE.Application.CoreCrm;
using Dapper;

namespace CURE.Infrastructure.Persistence;

public sealed class PostgresCoreCrmStore(PostgresConnectionFactory factory) : ICoreCrmStore
{
    public async Task<PageResult<OrganizationDto>> ListOrganizationsAsync(Guid t, PageQuery q, CancellationToken ct) => await List<OrganizationDto>(t, q, "organizations", "id AS Id, name AS Name, industry AS Industry, registration_number AS RegistrationNumber, updated_at AS UpdatedAt, version AS Version", "name", ct);
    public async Task<PageResult<ContactDto>> ListContactsAsync(Guid t, PageQuery q, CancellationToken ct) => await List<ContactDto>(t, q, "contacts", "id AS Id, organization_id AS OrganizationId, first_name AS FirstName, last_name AS LastName, email AS Email, phone AS Phone, role AS Role, updated_at AS UpdatedAt, version AS Version", "first_name || ' ' || last_name", ct);
    public async Task<PageResult<LeadDto>> ListLeadsAsync(Guid t, PageQuery q, CancellationToken ct) => await List<LeadDto>(t, q, "leads", "id AS Id, lead_number AS LeadNumber, first_name AS FirstName, last_name AS LastName, company_name AS CompanyName, email AS Email, status AS Status, estimated_value AS EstimatedValue, owner_id AS OwnerId, updated_at AS UpdatedAt, version AS Version", "first_name || ' ' || last_name || ' ' || coalesce(company_name, '')", ct);
    public async Task<PageResult<OpportunityDto>> ListOpportunitiesAsync(Guid t, PageQuery q, CancellationToken ct) => await List<OpportunityDto>(t, q, "opportunities", "id AS Id, opportunity_number AS OpportunityNumber, name AS Name, customer_id AS CustomerId, stage AS Stage, amount AS Amount, currency AS Currency, probability AS Probability, expected_close_date AS ExpectedCloseDate, owner_id AS OwnerId, updated_at AS UpdatedAt, version AS Version", "name", ct);
    public async Task<PageResult<ActivityDto>> ListActivitiesAsync(Guid t, PageQuery q, CancellationToken ct) => await List<ActivityDto>(t, q, "activities", "id AS Id, type AS Type, subject AS Subject, description AS Description, customer_id AS CustomerId, owner_id AS OwnerId, scheduled_at AS ScheduledAt, status AS Status, updated_at AS UpdatedAt, version AS Version", "subject", ct);
    public async Task<PageResult<CaseDto>> ListCasesAsync(Guid t, PageQuery q, CancellationToken ct) => await List<CaseDto>(t, q, "cases", "id AS Id, case_number AS CaseNumber, customer_id AS CustomerId, subject AS Subject, description AS Description, priority AS Priority, status AS Status, owner_id AS OwnerId, updated_at AS UpdatedAt, version AS Version", "subject", ct);
    public async Task<PageResult<SavedViewDto>> ListSavedViewsAsync(Guid t, PageQuery q, CancellationToken ct) => await List<SavedViewDto>(t, q, "saved_views", "id AS Id, name AS Name, resource_type AS ResourceType, configuration_json::text AS ConfigurationJson, shared AS Shared, updated_at AS UpdatedAt, version AS Version", "name", ct);

    private async Task<PageResult<T>> List<T>(Guid t, PageQuery q, string table, string columns, string searchColumn, CancellationToken ct)
    {
        await using var db = await factory.OpenForTenantAsync(t, ct);
        var search = string.IsNullOrWhiteSpace(q.Search) ? null : $"%{q.Search.Trim().ToLowerInvariant()}%";
        var offset = (q.Page - 1) * q.PageSize;
        var sql = $"SELECT {columns} FROM {table} WHERE tenant_id = @TenantId AND (@Search IS NULL OR lower({searchColumn}) LIKE @Search) ORDER BY updated_at DESC OFFSET @Offset LIMIT @PageSize; SELECT count(*) FROM {table} WHERE tenant_id = @TenantId AND (@Search IS NULL OR lower({searchColumn}) LIKE @Search);";
        using var grid = await db.QueryMultipleAsync(new CommandDefinition(sql, new { TenantId = t, Search = search, Offset = offset, q.PageSize }, cancellationToken: ct));
        return new PageResult<T>((await grid.ReadAsync<T>()).AsList(), q.Page, q.PageSize, await grid.ReadSingleAsync<int>());
    }

    public Task<OrganizationDto?> GetOrganizationAsync(Guid t, Guid id, CancellationToken c) => Single<OrganizationDto>(t, id, "organizations", "id AS Id, name AS Name, industry AS Industry, registration_number AS RegistrationNumber, updated_at AS UpdatedAt, version AS Version", c);
    public Task<ContactDto?> GetContactAsync(Guid t, Guid id, CancellationToken c) => Single<ContactDto>(t, id, "contacts", "id AS Id, organization_id AS OrganizationId, first_name AS FirstName, last_name AS LastName, email AS Email, phone AS Phone, role AS Role, updated_at AS UpdatedAt, version AS Version", c);
    public Task<LeadDto?> GetLeadAsync(Guid t, Guid id, CancellationToken c) => Single<LeadDto>(t, id, "leads", "id AS Id, lead_number AS LeadNumber, first_name AS FirstName, last_name AS LastName, company_name AS CompanyName, email AS Email, status AS Status, estimated_value AS EstimatedValue, owner_id AS OwnerId, updated_at AS UpdatedAt, version AS Version", c);
    public Task<OpportunityDto?> GetOpportunityAsync(Guid t, Guid id, CancellationToken c) => Single<OpportunityDto>(t, id, "opportunities", "id AS Id, opportunity_number AS OpportunityNumber, name AS Name, customer_id AS CustomerId, stage AS Stage, amount AS Amount, currency AS Currency, probability AS Probability, expected_close_date AS ExpectedCloseDate, owner_id AS OwnerId, updated_at AS UpdatedAt, version AS Version", c);
    public Task<ActivityDto?> GetActivityAsync(Guid t, Guid id, CancellationToken c) => Single<ActivityDto>(t, id, "activities", "id AS Id, type AS Type, subject AS Subject, description AS Description, customer_id AS CustomerId, owner_id AS OwnerId, scheduled_at AS ScheduledAt, status AS Status, updated_at AS UpdatedAt, version AS Version", c);
    public Task<CaseDto?> GetCaseAsync(Guid t, Guid id, CancellationToken c) => Single<CaseDto>(t, id, "cases", "id AS Id, case_number AS CaseNumber, customer_id AS CustomerId, subject AS Subject, description AS Description, priority AS Priority, status AS Status, owner_id AS OwnerId, updated_at AS UpdatedAt, version AS Version", c);
    public Task<SavedViewDto?> GetSavedViewAsync(Guid t, Guid id, CancellationToken c) => Single<SavedViewDto>(t, id, "saved_views", "id AS Id, name AS Name, resource_type AS ResourceType, configuration_json::text AS ConfigurationJson, shared AS Shared, updated_at AS UpdatedAt, version AS Version", c);
    private async Task<T?> Single<T>(Guid t, Guid id, string table, string columns, CancellationToken ct) where T : class { await using var db = await factory.OpenForTenantAsync(t, ct); return await db.QuerySingleOrDefaultAsync<T>(new CommandDefinition($"SELECT {columns} FROM {table} WHERE tenant_id = @TenantId AND id = @Id", new { TenantId = t, Id = id }, cancellationToken: ct)); }

    public async Task<OrganizationDto> CreateOrganizationAsync(Guid t, Guid a, string r, OrganizationCommand x, CancellationToken c) => await Execute<OrganizationDto>(t, a, r, "Organization", "organizations", "id AS Id, name AS Name, industry AS Industry, registration_number AS RegistrationNumber, updated_at AS UpdatedAt, version AS Version", "INSERT INTO organizations (id, tenant_id, name, normalized_name, industry, registration_number) VALUES (@Id,@TenantId,@Name,@NormalizedName,@Industry,@RegistrationNumber)", new { Id = Guid.CreateVersion7(), TenantId = t, Name = x.Name.Trim(), NormalizedName = x.Name.Trim().ToLowerInvariant(), x.Industry, x.RegistrationNumber }, c);
    public async Task<ContactDto> CreateContactAsync(Guid t, Guid a, string r, ContactCommand x, CancellationToken c) => await Execute<ContactDto>(t, a, r, "Contact", "contacts", "id AS Id, organization_id AS OrganizationId, first_name AS FirstName, last_name AS LastName, email AS Email, phone AS Phone, role AS Role, updated_at AS UpdatedAt, version AS Version", "INSERT INTO contacts (id, tenant_id, organization_id, first_name, last_name, email, phone, role) VALUES (@Id,@TenantId,@OrganizationId,@FirstName,@LastName,@Email,@Phone,@Role)", new { Id = Guid.CreateVersion7(), TenantId = t, x.OrganizationId, x.FirstName, x.LastName, x.Email, x.Phone, x.Role }, c);
    public async Task<LeadDto> CreateLeadAsync(Guid t, Guid a, string r, LeadCommand x, CancellationToken c) => await Execute<LeadDto>(t, a, r, "Lead", "leads", "id AS Id, lead_number AS LeadNumber, first_name AS FirstName, last_name AS LastName, company_name AS CompanyName, email AS Email, status AS Status, estimated_value AS EstimatedValue, owner_id AS OwnerId, updated_at AS UpdatedAt, version AS Version", "INSERT INTO leads (id, tenant_id, lead_number, first_name, last_name, company_name, email, phone, source, estimated_value, status, owner_id) VALUES (@Id,@TenantId,@Number,@FirstName,@LastName,@CompanyName,@Email,@Phone,@Source,@EstimatedValue,@Status,@OwnerId)", new { Id = Guid.CreateVersion7(), TenantId = t, Number = "LEAD-" + DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(), x.FirstName, x.LastName, x.CompanyName, x.Email, x.Phone, x.Source, x.EstimatedValue, x.Status, x.OwnerId }, c);
    public async Task<OpportunityDto> CreateOpportunityAsync(Guid t, Guid a, string r, OpportunityCommand x, CancellationToken c) => await Execute<OpportunityDto>(t, a, r, "Opportunity", "opportunities", "id AS Id, opportunity_number AS OpportunityNumber, name AS Name, customer_id AS CustomerId, stage AS Stage, amount AS Amount, currency AS Currency, probability AS Probability, expected_close_date AS ExpectedCloseDate, owner_id AS OwnerId, updated_at AS UpdatedAt, version AS Version", "INSERT INTO opportunities (id, tenant_id, opportunity_number, customer_id, name, stage, amount, currency, probability, expected_close_date, owner_id) VALUES (@Id,@TenantId,@Number,@CustomerId,@Name,@Stage,@Amount,@Currency,@Probability,@ExpectedCloseDate,@OwnerId)", new { Id = Guid.CreateVersion7(), TenantId = t, Number = "OPP-" + DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(), x.CustomerId, x.Name, x.Stage, x.Amount, x.Currency, x.Probability, x.ExpectedCloseDate, x.OwnerId }, c);
    public async Task<ActivityDto> CreateActivityAsync(Guid t, Guid a, string r, ActivityCommand x, CancellationToken c) => await Execute<ActivityDto>(t, a, r, "Activity", "activities", "id AS Id, type AS Type, subject AS Subject, description AS Description, customer_id AS CustomerId, owner_id AS OwnerId, scheduled_at AS ScheduledAt, status AS Status, updated_at AS UpdatedAt, version AS Version", "INSERT INTO activities (id, tenant_id, type, subject, description, customer_id, owner_id, scheduled_at, status) VALUES (@Id,@TenantId,@Type,@Subject,@Description,@CustomerId,@OwnerId,@ScheduledAt,@Status)", new { Id = Guid.CreateVersion7(), TenantId = t, x.Type, x.Subject, x.Description, x.CustomerId, x.OwnerId, x.ScheduledAt, x.Status }, c);
    public async Task<CaseDto> CreateCaseAsync(Guid t, Guid a, string r, CaseCommand x, CancellationToken c) => await Execute<CaseDto>(t, a, r, "Case", "cases", "id AS Id, case_number AS CaseNumber, customer_id AS CustomerId, subject AS Subject, description AS Description, priority AS Priority, status AS Status, owner_id AS OwnerId, updated_at AS UpdatedAt, version AS Version", "INSERT INTO cases (id, tenant_id, case_number, customer_id, subject, description, priority, status, owner_id) VALUES (@Id,@TenantId,@Number,@CustomerId,@Subject,@Description,@Priority,@Status,@OwnerId)", new { Id = Guid.CreateVersion7(), TenantId = t, Number = "CASE-" + DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(), x.CustomerId, x.Subject, x.Description, x.Priority, x.Status, x.OwnerId }, c);
    public async Task<SavedViewDto> CreateSavedViewAsync(Guid t, Guid a, string r, SavedViewCommand x, CancellationToken c) => await Execute<SavedViewDto>(t, a, r, "SavedView", "saved_views", "id AS Id, name AS Name, resource_type AS ResourceType, configuration_json::text AS ConfigurationJson, shared AS Shared, updated_at AS UpdatedAt, version AS Version", "INSERT INTO saved_views (id, tenant_id, owner_id, name, resource_type, configuration_json, shared) VALUES (@Id,@TenantId,@OwnerId,@Name,@ResourceType,CAST(@ConfigurationJson AS jsonb),@Shared)", new { Id = Guid.CreateVersion7(), TenantId = t, OwnerId = a, x.Name, x.ResourceType, x.ConfigurationJson, x.Shared }, c);

    private async Task<T> Execute<T>(Guid t, Guid a, string r, string entity, string table, string columns, string sql, object args, CancellationToken ct)
        where T : class
    {
        var id = (Guid)args.GetType().GetProperty("Id")!.GetValue(args)!;
        await using var db = await factory.OpenForTenantAsync(t, ct);
        await using var transaction = await db.BeginTransactionAsync(ct);
        await db.ExecuteAsync(new CommandDefinition(sql, args, transaction, cancellationToken: ct));
        var after = await db.QuerySingleAsync<T>(new CommandDefinition(
            $"SELECT {columns} FROM {table} WHERE tenant_id=@TenantId AND id=@Id",
            new { TenantId = t, Id = id }, transaction, cancellationToken: ct));
        await WriteAuditAsync(db, transaction, t, a, r, entity.ToUpperInvariant() + "_CREATED", entity, id, null, after, ct);
        await transaction.CommitAsync(ct);
        return after;
    }

    public async Task<OrganizationDto?> UpdateOrganizationAsync(Guid t, Guid a, string r, Guid id, OrganizationCommand x, CancellationToken c) => await Update<OrganizationDto>(t, a, r, id, x.ExpectedVersion, "organizations", "id AS Id, name AS Name, industry AS Industry, registration_number AS RegistrationNumber, updated_at AS UpdatedAt, version AS Version", "name=@Name, normalized_name=@NormalizedName, industry=@Industry, registration_number=@RegistrationNumber", new { x.Name, NormalizedName = x.Name.Trim().ToLowerInvariant(), x.Industry, x.RegistrationNumber }, c);
    public async Task<ContactDto?> UpdateContactAsync(Guid t, Guid a, string r, Guid id, ContactCommand x, CancellationToken c) => await Update<ContactDto>(t, a, r, id, x.ExpectedVersion, "contacts", "id AS Id, organization_id AS OrganizationId, first_name AS FirstName, last_name AS LastName, email AS Email, phone AS Phone, role AS Role, updated_at AS UpdatedAt, version AS Version", "organization_id=@OrganizationId, first_name=@FirstName, last_name=@LastName, email=@Email, phone=@Phone, role=@Role", x, c);
    public async Task<LeadDto?> UpdateLeadAsync(Guid t, Guid a, string r, Guid id, LeadCommand x, CancellationToken c) => await Update<LeadDto>(t, a, r, id, x.ExpectedVersion, "leads", "id AS Id, lead_number AS LeadNumber, first_name AS FirstName, last_name AS LastName, company_name AS CompanyName, email AS Email, status AS Status, estimated_value AS EstimatedValue, owner_id AS OwnerId, updated_at AS UpdatedAt, version AS Version", "first_name=@FirstName, last_name=@LastName, company_name=@CompanyName, email=@Email, phone=@Phone, source=@Source, estimated_value=@EstimatedValue, status=@Status, owner_id=@OwnerId", x, c);
    public async Task<OpportunityDto?> UpdateOpportunityAsync(Guid t, Guid a, string r, Guid id, OpportunityCommand x, CancellationToken c) => await Update<OpportunityDto>(t, a, r, id, x.ExpectedVersion, "opportunities", "id AS Id, opportunity_number AS OpportunityNumber, name AS Name, customer_id AS CustomerId, stage AS Stage, amount AS Amount, currency AS Currency, probability AS Probability, expected_close_date AS ExpectedCloseDate, owner_id AS OwnerId, updated_at AS UpdatedAt, version AS Version", "customer_id=@CustomerId, name=@Name, stage=@Stage, amount=@Amount, currency=@Currency, probability=@Probability, expected_close_date=@ExpectedCloseDate, owner_id=@OwnerId", x, c);
    public async Task<ActivityDto?> UpdateActivityAsync(Guid t, Guid a, string r, Guid id, ActivityCommand x, CancellationToken c) => await Update<ActivityDto>(t, a, r, id, x.ExpectedVersion, "activities", "id AS Id, type AS Type, subject AS Subject, description AS Description, customer_id AS CustomerId, owner_id AS OwnerId, scheduled_at AS ScheduledAt, status AS Status, updated_at AS UpdatedAt, version AS Version", "type=@Type, subject=@Subject, description=@Description, customer_id=@CustomerId, owner_id=@OwnerId, scheduled_at=@ScheduledAt, status=@Status", x, c);
    public async Task<CaseDto?> UpdateCaseAsync(Guid t, Guid a, string r, Guid id, CaseCommand x, CancellationToken c) => await Update<CaseDto>(t, a, r, id, x.ExpectedVersion, "cases", "id AS Id, case_number AS CaseNumber, customer_id AS CustomerId, subject AS Subject, description AS Description, priority AS Priority, status AS Status, owner_id AS OwnerId, updated_at AS UpdatedAt, version AS Version", "customer_id=@CustomerId, subject=@Subject, description=@Description, priority=@Priority, status=@Status, owner_id=@OwnerId", x, c);
    public async Task<SavedViewDto?> UpdateSavedViewAsync(Guid t, Guid a, string r, Guid id, SavedViewCommand x, CancellationToken c) => await Update<SavedViewDto>(t, a, r, id, x.ExpectedVersion, "saved_views", "id AS Id, name AS Name, resource_type AS ResourceType, configuration_json::text AS ConfigurationJson, shared AS Shared, updated_at AS UpdatedAt, version AS Version", "name=@Name, resource_type=@ResourceType, configuration_json=CAST(@ConfigurationJson AS jsonb), shared=@Shared", x, c);

    private async Task<T?> Update<T>(Guid t, Guid a, string r, Guid id, int? version, string table, string columns, string set, object args, CancellationToken ct)
        where T : class
    {
        await using var db = await factory.OpenForTenantAsync(t, ct);
        await using var transaction = await db.BeginTransactionAsync(ct);
        var before = await db.QuerySingleOrDefaultAsync<T>(new CommandDefinition(
            $"SELECT {columns} FROM {table} WHERE tenant_id=@TenantId AND id=@Id FOR UPDATE",
            new { TenantId = t, Id = id }, transaction, cancellationToken: ct));
        if (before is null)
        {
            await transaction.CommitAsync(ct);
            return null;
        }

        var parameters = new DynamicParameters(args);
        parameters.Add("TenantId", t);
        parameters.Add("Id", id);
        parameters.Add("ExpectedVersion", version);
        var changed = await db.ExecuteAsync(new CommandDefinition(
            $"UPDATE {table} SET {set}, updated_at=now(), version=version+1 WHERE tenant_id=@TenantId AND id=@Id AND (@ExpectedVersion IS NULL OR version=@ExpectedVersion)",
            parameters, transaction, cancellationToken: ct));
        if (changed == 0)
        {
            await transaction.CommitAsync(ct);
            return null;
        }

        var after = await db.QuerySingleAsync<T>(new CommandDefinition(
            $"SELECT {columns} FROM {table} WHERE tenant_id=@TenantId AND id=@Id",
            new { TenantId = t, Id = id }, transaction, cancellationToken: ct));
        await WriteAuditAsync(db, transaction, t, a, r, table.ToUpperInvariant() + "_UPDATED", table, id, before, after, ct);
        await transaction.CommitAsync(ct);
        return after;
    }

    public async Task<bool> DeleteAsync(Guid t, Guid a, string r, string resource, Guid id, CancellationToken ct)
    {
        var (table, columns) = resource switch
        {
            "organizations" => ("organizations", "id AS Id, name AS Name, industry AS Industry, registration_number AS RegistrationNumber, updated_at AS UpdatedAt, version AS Version"),
            "contacts" => ("contacts", "id AS Id, organization_id AS OrganizationId, first_name AS FirstName, last_name AS LastName, email AS Email, phone AS Phone, role AS Role, updated_at AS UpdatedAt, version AS Version"),
            "leads" => ("leads", "id AS Id, lead_number AS LeadNumber, first_name AS FirstName, last_name AS LastName, company_name AS CompanyName, email AS Email, status AS Status, estimated_value AS EstimatedValue, owner_id AS OwnerId, updated_at AS UpdatedAt, version AS Version"),
            "opportunities" => ("opportunities", "id AS Id, opportunity_number AS OpportunityNumber, name AS Name, customer_id AS CustomerId, stage AS Stage, amount AS Amount, currency AS Currency, probability AS Probability, expected_close_date AS ExpectedCloseDate, owner_id AS OwnerId, updated_at AS UpdatedAt, version AS Version"),
            "activities" => ("activities", "id AS Id, type AS Type, subject AS Subject, description AS Description, customer_id AS CustomerId, owner_id AS OwnerId, scheduled_at AS ScheduledAt, status AS Status, updated_at AS UpdatedAt, version AS Version"),
            "cases" => ("cases", "id AS Id, case_number AS CaseNumber, customer_id AS CustomerId, subject AS Subject, description AS Description, priority AS Priority, status AS Status, owner_id AS OwnerId, updated_at AS UpdatedAt, version AS Version"),
            "saved-views" => ("saved_views", "id AS Id, name AS Name, resource_type AS ResourceType, configuration_json::text AS ConfigurationJson, shared AS Shared, updated_at AS UpdatedAt, version AS Version"),
            _ => throw new ArgumentException("Unknown resource.", nameof(resource)),
        };
        await using var db = await factory.OpenForTenantAsync(t, ct);
        await using var transaction = await db.BeginTransactionAsync(ct);
        var before = await db.QuerySingleOrDefaultAsync<dynamic>(new CommandDefinition(
            $"SELECT {columns} FROM {table} WHERE tenant_id=@TenantId AND id=@Id FOR UPDATE",
            new { TenantId = t, Id = id }, transaction, cancellationToken: ct));
        if (before is null)
        {
            await transaction.CommitAsync(ct);
            return false;
        }

        await db.ExecuteAsync(new CommandDefinition(
            $"DELETE FROM {table} WHERE tenant_id=@TenantId AND id=@Id",
            new { TenantId = t, Id = id }, transaction, cancellationToken: ct));
        await WriteAuditAsync(db, transaction, t, a, r, resource.ToUpperInvariant() + "_DELETED", resource, id, before, null, ct);
        await transaction.CommitAsync(ct);
        return true;
    }

    private static async Task WriteAuditAsync(
        Npgsql.NpgsqlConnection db,
        Npgsql.NpgsqlTransaction transaction,
        Guid tenantId,
        Guid actorId,
        string requestId,
        string action,
        string entityType,
        Guid entityId,
        object? before,
        object? after,
        CancellationToken ct)
    {
        await db.ExecuteAsync(new CommandDefinition(
            "INSERT INTO audit_events (id, tenant_id, actor_id, action, entity_type, entity_id, request_id, before_state, after_state) VALUES (@Id,@TenantId,@ActorId,@Action,@EntityType,@EntityId,@RequestId,CAST(@Before AS jsonb),CAST(@After AS jsonb))",
            new { Id = Guid.CreateVersion7(), TenantId = tenantId, ActorId = actorId, Action = action, EntityType = entityType, EntityId = entityId, RequestId = requestId, Before = before is null ? null : JsonSerializer.Serialize(before), After = after is null ? null : JsonSerializer.Serialize(after) },
            transaction, cancellationToken: ct));
    }
}
