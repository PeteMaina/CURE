using System.Text.Json;
using CURE.Application.Customers;
using CURE.Application.Workspace;
using Dapper;

namespace CURE.Infrastructure.Persistence;

public sealed class PostgresWorkspaceStore(PostgresConnectionFactory factory) : IWorkspaceStore
{
    private const string AttentionCte = """
        WITH attention AS (
            SELECT
                'activity'::text AS "EntityType",
                a.id AS "EntityId",
                'Due activity'::text AS "Category",
                a.subject AS "Title",
                a.description AS "Description",
                a.customer_id AS "CustomerId",
                c.name AS "CustomerName",
                a.status AS "Status",
                NULL::text AS "Priority",
                a.scheduled_at AS "DueAt",
                a.updated_at AS "UpdatedAt"
            FROM activities a
            LEFT JOIN customers c ON c.id = a.customer_id AND c.tenant_id = a.tenant_id
            WHERE a.tenant_id = @TenantId
              AND upper(a.status) IN ('PLANNED', 'IN_PROGRESS')
              AND a.scheduled_at IS NOT NULL
              AND a.scheduled_at <= now()

            UNION ALL

            SELECT
                'case'::text AS "EntityType",
                cs.id AS "EntityId",
                'Open case'::text AS "Category",
                cs.subject AS "Title",
                cs.description AS "Description",
                cs.customer_id AS "CustomerId",
                c.name AS "CustomerName",
                cs.status AS "Status",
                cs.priority AS "Priority",
                NULL::timestamptz AS "DueAt",
                cs.updated_at AS "UpdatedAt"
            FROM cases cs
            LEFT JOIN customers c ON c.id = cs.customer_id AND c.tenant_id = cs.tenant_id
            WHERE cs.tenant_id = @TenantId
              AND upper(cs.status) NOT IN ('RESOLVED', 'CLOSED')
        )
        """;

    public async Task<HomeDashboard> GetDashboardAsync(Guid tenantId, Guid userId, CancellationToken ct)
    {
        await using var db = await factory.OpenForTenantAsync(tenantId, ct);
        const string sql = """
            SELECT display_name
            FROM users
            WHERE tenant_id = @TenantId AND id = @UserId;

            SELECT
                (SELECT count(*)::int FROM customers WHERE tenant_id = @TenantId) AS Customers,
                (SELECT count(*)::int FROM leads WHERE tenant_id = @TenantId) AS Leads,
                (SELECT count(*)::int FROM opportunities WHERE tenant_id = @TenantId) AS Opportunities,
                (SELECT count(*)::int FROM cases WHERE tenant_id = @TenantId AND upper(status) NOT IN ('RESOLVED', 'CLOSED')) AS OpenCases,
                (SELECT count(*)::int FROM activities WHERE tenant_id = @TenantId AND upper(status) IN ('PLANNED', 'IN_PROGRESS') AND scheduled_at IS NOT NULL AND scheduled_at <= now()) AS DueActivities;

            SELECT c.id AS Id, c.name AS Name, c.customer_type AS Type, c.industry AS Industry,
                   c.owner_id AS OwnerId, u.display_name AS OwnerName, c.updated_at AS UpdatedAt, c.version AS Version
            FROM customers c
            LEFT JOIN users u ON u.id = c.owner_id AND u.tenant_id = c.tenant_id
            WHERE c.tenant_id = @TenantId
            ORDER BY c.updated_at DESC
            LIMIT 6;

            SELECT a.id AS Id, a.type AS Type, a.subject AS Subject, a.status AS Status,
                   a.scheduled_at AS ScheduledAt, a.customer_id AS CustomerId, c.name AS CustomerName
            FROM activities a
            LEFT JOIN customers c ON c.id = a.customer_id AND c.tenant_id = a.tenant_id
            WHERE a.tenant_id = @TenantId
            ORDER BY a.updated_at DESC
            LIMIT 8;
            """;

        using var grid = await db.QueryMultipleAsync(
            new CommandDefinition(sql, new { TenantId = tenantId, UserId = userId }, cancellationToken: ct));

        var userName = await grid.ReadSingleOrDefaultAsync<string>() ?? "there";
        var counts = await grid.ReadSingleAsync<WorkspaceCounts>();
        var recentCustomers = (await grid.ReadAsync<CustomerListItem>()).AsList();
        var recentActivities = (await grid.ReadAsync<WorkspaceActivity>()).AsList();
        var inbox = await GetInboxPageAsync(db, tenantId, userId, null, 1, 6, ct);

        return new HomeDashboard(
            userName,
            DateTimeOffset.UtcNow,
            counts,
            inbox.Data,
            recentCustomers,
            recentActivities);
    }

    public async Task<InboxPage> GetInboxAsync(
        Guid tenantId,
        Guid userId,
        string? category,
        int page,
        int pageSize,
        CancellationToken ct)
    {
        await using var db = await factory.OpenForTenantAsync(tenantId, ct);
        return await GetInboxPageAsync(db, tenantId, userId, category, page, pageSize, ct);
    }

    private static async Task<InboxPage> GetInboxPageAsync(
        Npgsql.NpgsqlConnection db,
        Guid tenantId,
        Guid userId,
        string? category,
        int page,
        int pageSize,
        CancellationToken ct)
    {
        var offset = (page - 1) * pageSize;
        var sql = $"""
            {AttentionCte}
            SELECT a."EntityType", a."EntityId", a."Category", a."Title", a."Description",
                   a."CustomerId", a."CustomerName", a."Status", a."Priority", a."DueAt",
                   a."UpdatedAt", (ns.read_at IS NOT NULL) AS "IsRead",
                   count(*) OVER()::int AS "Total"
            FROM attention a
            LEFT JOIN notification_states ns
              ON ns.tenant_id = @TenantId
             AND ns.user_id = @UserId
             AND ns.entity_type = a."EntityType"
             AND ns.entity_id = a."EntityId"
            WHERE (@Category IS NULL OR a."EntityType" = @Category)
            ORDER BY a."DueAt" ASC NULLS LAST, a."UpdatedAt" ASC
            OFFSET @Offset LIMIT @PageSize;

            {AttentionCte}
            SELECT
                count(*) FILTER (WHERE a."EntityType" = 'activity')::int AS "DueActivityCount",
                count(*) FILTER (WHERE a."EntityType" = 'case')::int AS "OpenCaseCount",
                count(*)::int AS "Total"
            FROM attention a
            WHERE (@Category IS NULL OR a."EntityType" = @Category);
            """;

        using var grid = await db.QueryMultipleAsync(new CommandDefinition(
            sql,
            new { TenantId = tenantId, UserId = userId, Category = category, Offset = offset, PageSize = pageSize },
            cancellationToken: ct));

        var rows = (await grid.ReadAsync<InboxRow>()).AsList();
        var counts = await grid.ReadSingleAsync<InboxCounts>();
        var items = rows.Select(row => new InboxItem(
            row.EntityType,
            row.EntityId,
            row.Category,
            row.Title,
            row.Description,
            row.CustomerId,
            row.CustomerName,
            row.Status,
            row.Priority,
            row.DueAt,
            row.UpdatedAt,
            row.IsRead)).ToArray();

        return new InboxPage(
            items,
            page,
            pageSize,
            rows.Count > 0 ? rows[0].Total : counts.Total,
            counts.DueActivityCount,
            counts.OpenCaseCount);
    }

    public async Task<Customer360View?> GetCustomer360Async(Guid tenantId, Guid customerId, CancellationToken ct)
    {
        await using var db = await factory.OpenForTenantAsync(tenantId, ct);
        var customer = await db.QuerySingleOrDefaultAsync<CustomerDetail>(new CommandDefinition(
            """
            SELECT id AS Id, name AS Name, customer_type AS Type, industry AS Industry, email AS Email,
                   phone AS Phone, owner_id AS OwnerId, created_at AS CreatedAt, updated_at AS UpdatedAt, version AS Version
            FROM customers
            WHERE tenant_id = @TenantId AND id = @CustomerId;
            """,
            new { TenantId = tenantId, CustomerId = customerId },
            cancellationToken: ct));

        if (customer is null)
        {
            return null;
        }

        var contacts = (await db.QueryAsync<CustomerContactView>(new CommandDefinition(
            """
            SELECT c.id AS Id, c.first_name AS FirstName, c.last_name AS LastName,
                   c.email AS Email, c.phone AS Phone, c.role AS Role, link.linked_at AS LinkedAt
            FROM customer_contact_links link
            JOIN contacts c ON c.id = link.contact_id AND c.tenant_id = link.tenant_id
            WHERE link.tenant_id = @TenantId AND link.customer_id = @CustomerId
            ORDER BY lower(c.last_name), lower(c.first_name)
            LIMIT 100;
            """,
            new { TenantId = tenantId, CustomerId = customerId },
            cancellationToken: ct))).AsList();

        var opportunities = (await db.QueryAsync<RelatedOpportunityView>(new CommandDefinition(
            """
            SELECT id AS Id, opportunity_number AS OpportunityNumber, name AS Name,
                   stage AS Stage, amount AS Amount, currency AS Currency,
                   expected_close_date AS ExpectedCloseDate, updated_at AS UpdatedAt
            FROM opportunities
            WHERE tenant_id = @TenantId AND customer_id = @CustomerId
            ORDER BY updated_at DESC
            LIMIT 100;
            """,
            new { TenantId = tenantId, CustomerId = customerId },
            cancellationToken: ct))).AsList();

        var cases = (await db.QueryAsync<RelatedCaseView>(new CommandDefinition(
            """
            SELECT id AS Id, case_number AS CaseNumber, subject AS Subject,
                   priority AS Priority, status AS Status, updated_at AS UpdatedAt
            FROM cases
            WHERE tenant_id = @TenantId AND customer_id = @CustomerId
            ORDER BY updated_at DESC
            LIMIT 100;
            """,
            new { TenantId = tenantId, CustomerId = customerId },
            cancellationToken: ct))).AsList();

        var activities = (await db.QueryAsync<RelatedActivityView>(new CommandDefinition(
            """
            SELECT id AS Id, type AS Type, subject AS Subject, description AS Description,
                   status AS Status, scheduled_at AS ScheduledAt, updated_at AS UpdatedAt
            FROM activities
            WHERE tenant_id = @TenantId AND customer_id = @CustomerId
            ORDER BY coalesce(scheduled_at, updated_at) DESC
            LIMIT 100;
            """,
            new { TenantId = tenantId, CustomerId = customerId },
            cancellationToken: ct))).AsList();

        var metrics = await db.QuerySingleAsync<Customer360MetricRow>(new CommandDefinition(
            """
            SELECT
                (SELECT count(*)::int FROM customer_contact_links
                 WHERE tenant_id = @TenantId AND customer_id = @CustomerId) AS Contacts,
                (SELECT count(*)::int FROM opportunities
                 WHERE tenant_id = @TenantId AND customer_id = @CustomerId) AS Opportunities,
                (SELECT count(*)::int FROM cases
                 WHERE tenant_id = @TenantId AND customer_id = @CustomerId) AS Cases,
                (SELECT count(*)::int FROM cases
                 WHERE tenant_id = @TenantId AND customer_id = @CustomerId
                   AND upper(status) NOT IN ('RESOLVED', 'CLOSED')) AS OpenCases,
                (SELECT count(*)::int FROM activities
                 WHERE tenant_id = @TenantId AND customer_id = @CustomerId) AS Activities;
            """,
            new { TenantId = tenantId, CustomerId = customerId },
            cancellationToken: ct));

        var timeline = (await db.QueryAsync<CustomerTimelineEntry>(new CommandDefinition(
            """
            SELECT id AS Id, entity_type AS EntityType, action AS Action,
                   entity_id AS EntityId, actor_id AS ActorId, occurred_at AS OccurredAt
            FROM audit_events
            WHERE tenant_id = @TenantId
              AND (
                  entity_id = @CustomerId
                  OR entity_id IN (
                      SELECT contact_id FROM customer_contact_links
                      WHERE tenant_id = @TenantId AND customer_id = @CustomerId
                  )
                  OR entity_id IN (
                      SELECT id FROM opportunities
                      WHERE tenant_id = @TenantId AND customer_id = @CustomerId
                  )
                  OR entity_id IN (
                      SELECT id FROM cases
                      WHERE tenant_id = @TenantId AND customer_id = @CustomerId
                  )
                  OR entity_id IN (
                      SELECT id FROM activities
                      WHERE tenant_id = @TenantId AND customer_id = @CustomerId
                  )
              )
            ORDER BY occurred_at DESC
            LIMIT 100;
            """,
            new { TenantId = tenantId, CustomerId = customerId },
            cancellationToken: ct))).AsList();

        return new Customer360View(
            customer,
            new Customer360Metrics(
                metrics.Contacts,
                metrics.Opportunities,
                metrics.Cases,
                metrics.OpenCases,
                metrics.Activities),
            contacts,
            opportunities,
            cases,
            activities,
            timeline);
    }

    public async Task<IReadOnlyList<SearchHit>> SearchAsync(
        Guid tenantId,
        string query,
        int limit,
        CancellationToken ct)
    {
        var escaped = query.Trim().ToLowerInvariant()
            .Replace("!", "!!", StringComparison.Ordinal)
            .Replace("%", "!%", StringComparison.Ordinal)
            .Replace("_", "!_", StringComparison.Ordinal);
        var pattern = $"%{escaped}%";
        await using var db = await factory.OpenForTenantAsync(tenantId, ct);
        const string sql = """
            SELECT *
            FROM (
                SELECT 'customer'::text AS EntityType, id AS Id, name AS Title,
                       coalesce(industry, customer_type) AS Detail, updated_at AS UpdatedAt
                FROM customers
                WHERE tenant_id = @TenantId
                  AND (lower(name) LIKE @Pattern ESCAPE '!' OR lower(coalesce(email, '')) LIKE @Pattern ESCAPE '!')

                UNION ALL

                SELECT 'organization', id, name, coalesce(industry, registration_number), updated_at
                FROM organizations
                WHERE tenant_id = @TenantId
                  AND (lower(name) LIKE @Pattern ESCAPE '!' OR lower(coalesce(industry, '')) LIKE @Pattern ESCAPE '!')

                UNION ALL

                SELECT 'contact', id, first_name || ' ' || last_name, coalesce(email, role), updated_at
                FROM contacts
                WHERE tenant_id = @TenantId
                  AND (lower(first_name || ' ' || last_name) LIKE @Pattern ESCAPE '!' OR lower(coalesce(email, '')) LIKE @Pattern ESCAPE '!')

                UNION ALL

                SELECT 'lead', id, first_name || ' ' || last_name, coalesce(company_name, email), updated_at
                FROM leads
                WHERE tenant_id = @TenantId
                  AND (lower(first_name || ' ' || last_name) LIKE @Pattern ESCAPE '!' OR lower(coalesce(company_name, '')) LIKE @Pattern ESCAPE '!')

                UNION ALL

                SELECT 'opportunity', id, name, stage, updated_at
                FROM opportunities
                WHERE tenant_id = @TenantId AND lower(name) LIKE @Pattern ESCAPE '!'

                UNION ALL

                SELECT 'activity', id, subject, type, updated_at
                FROM activities
                WHERE tenant_id = @TenantId AND lower(subject) LIKE @Pattern ESCAPE '!'

                UNION ALL

                SELECT 'case', id, subject, priority, updated_at
                FROM cases
                WHERE tenant_id = @TenantId AND lower(subject) LIKE @Pattern ESCAPE '!'
            ) matches
            ORDER BY UpdatedAt DESC
            LIMIT @Limit;
            """;

        var results = await db.QueryAsync<SearchHit>(new CommandDefinition(
            sql,
            new { TenantId = tenantId, Pattern = pattern, Limit = limit },
            cancellationToken: ct));
        return results.AsList();
    }

    public async Task<NotificationFeed> GetNotificationsAsync(
        Guid tenantId,
        Guid userId,
        int limit,
        CancellationToken ct)
    {
        await using var db = await factory.OpenForTenantAsync(tenantId, ct);
        var sql = $"""
            {AttentionCte}
            SELECT a."EntityType", a."EntityId", a."Category", a."Title", a."Description",
                   a."CustomerId", a."CustomerName", a."Status", a."Priority", a."DueAt",
                   a."UpdatedAt", (ns.read_at IS NOT NULL) AS "IsRead"
            FROM attention a
            LEFT JOIN notification_states ns
              ON ns.tenant_id = @TenantId
             AND ns.user_id = @UserId
             AND ns.entity_type = a."EntityType"
             AND ns.entity_id = a."EntityId"
            ORDER BY (ns.read_at IS NULL) DESC, a."DueAt" ASC NULLS LAST, a."UpdatedAt" DESC
            LIMIT @Limit;

            {AttentionCte}
            SELECT count(*)::int
            FROM attention a
            LEFT JOIN notification_states ns
              ON ns.tenant_id = @TenantId
             AND ns.user_id = @UserId
             AND ns.entity_type = a."EntityType"
             AND ns.entity_id = a."EntityId"
            WHERE ns.read_at IS NULL;
            """;

        using var grid = await db.QueryMultipleAsync(new CommandDefinition(
            sql,
            new { TenantId = tenantId, UserId = userId, Limit = limit },
            cancellationToken: ct));

        var notifications = (await grid.ReadAsync<InboxItem>()).AsList();
        var unreadCount = await grid.ReadSingleAsync<int>();
        return new NotificationFeed(notifications, unreadCount);
    }

    public async Task SetNotificationReadAsync(
        Guid tenantId,
        Guid userId,
        string entityType,
        Guid entityId,
        bool isRead,
        CancellationToken ct)
    {
        await using var db = await factory.OpenForTenantAsync(tenantId, ct);
        await db.ExecuteAsync(new CommandDefinition(
            """
            INSERT INTO notification_states (tenant_id, user_id, entity_type, entity_id, read_at, updated_at)
            VALUES (@TenantId, @UserId, @EntityType, @EntityId, @ReadAt, now())
            ON CONFLICT (tenant_id, user_id, entity_type, entity_id)
            DO UPDATE SET read_at = EXCLUDED.read_at, updated_at = now();
            """,
            new
            {
                TenantId = tenantId,
                UserId = userId,
                EntityType = entityType,
                EntityId = entityId,
                ReadAt = isRead ? DateTimeOffset.UtcNow : (DateTimeOffset?)null,
            },
            cancellationToken: ct));
    }

    public async Task LinkContactAsync(
        Guid tenantId,
        Guid actorId,
        string requestId,
        Guid customerId,
        Guid contactId,
        CancellationToken ct)
    {
        await using var db = await factory.OpenForTenantAsync(tenantId, ct);
        await using var transaction = await db.BeginTransactionAsync(ct);

        var recordsExist = await db.QuerySingleAsync<bool>(new CommandDefinition(
            """
            SELECT EXISTS (SELECT 1 FROM customers WHERE tenant_id = @TenantId AND id = @CustomerId)
               AND EXISTS (SELECT 1 FROM contacts WHERE tenant_id = @TenantId AND id = @ContactId);
            """,
            new { TenantId = tenantId, CustomerId = customerId, ContactId = contactId },
            transaction,
            cancellationToken: ct));

        if (!recordsExist)
        {
            throw new CURE.Domain.Shared.DomainException(
                CURE.Domain.Shared.ErrorCodes.NotFound,
                "The customer or contact could not be found.");
        }

        var inserted = await db.ExecuteScalarAsync<bool?>(new CommandDefinition(
            """
            INSERT INTO customer_contact_links (tenant_id, customer_id, contact_id)
            VALUES (@TenantId, @CustomerId, @ContactId)
            ON CONFLICT (tenant_id, customer_id, contact_id) DO NOTHING
            RETURNING true;
            """,
            new { TenantId = tenantId, CustomerId = customerId, ContactId = contactId },
            transaction,
            cancellationToken: ct));

        if (inserted is true)
        {
            await WriteRelationshipAuditAsync(
                db,
                transaction,
                tenantId,
                actorId,
                requestId,
                customerId,
                "CONTACT_LINKED",
                new { contactId },
                ct);
        }

        await transaction.CommitAsync(ct);
    }

    public async Task UnlinkContactAsync(
        Guid tenantId,
        Guid actorId,
        string requestId,
        Guid customerId,
        Guid contactId,
        CancellationToken ct)
    {
        await using var db = await factory.OpenForTenantAsync(tenantId, ct);
        await using var transaction = await db.BeginTransactionAsync(ct);
        var deleted = await db.ExecuteAsync(new CommandDefinition(
            """
            DELETE FROM customer_contact_links
            WHERE tenant_id = @TenantId AND customer_id = @CustomerId AND contact_id = @ContactId;
            """,
            new { TenantId = tenantId, CustomerId = customerId, ContactId = contactId },
            transaction,
            cancellationToken: ct));

        if (deleted == 0)
        {
            throw new CURE.Domain.Shared.DomainException(
                CURE.Domain.Shared.ErrorCodes.NotFound,
                "The customer-contact relationship could not be found.");
        }

        await WriteRelationshipAuditAsync(
            db,
            transaction,
            tenantId,
            actorId,
            requestId,
            customerId,
            "CONTACT_UNLINKED",
            new { contactId },
            ct);
        await transaction.CommitAsync(ct);
    }

    private static Task WriteRelationshipAuditAsync(
        Npgsql.NpgsqlConnection db,
        Npgsql.NpgsqlTransaction transaction,
        Guid tenantId,
        Guid actorId,
        string requestId,
        Guid customerId,
        string action,
        object details,
        CancellationToken ct)
        => db.ExecuteAsync(new CommandDefinition(
            """
            INSERT INTO audit_events
                (id, tenant_id, actor_id, action, entity_type, entity_id, request_id, after_state)
            VALUES
                (@Id, @TenantId, @ActorId, @Action, 'CUSTOMER_CONTACT', @CustomerId, @RequestId, CAST(@After AS jsonb));
            """,
            new
            {
                Id = Guid.CreateVersion7(),
                TenantId = tenantId,
                ActorId = actorId,
                Action = action,
                CustomerId = customerId,
                RequestId = requestId,
                After = JsonSerializer.Serialize(details),
            },
            transaction,
            cancellationToken: ct));

    private sealed record InboxRow(
        string EntityType,
        Guid EntityId,
        string Category,
        string Title,
        string? Description,
        Guid? CustomerId,
        string? CustomerName,
        string Status,
        string? Priority,
        DateTimeOffset? DueAt,
        DateTimeOffset UpdatedAt,
        bool IsRead,
        int Total);

    private sealed record InboxCounts(int DueActivityCount, int OpenCaseCount, int Total);
    private sealed record Customer360MetricRow(
        int Contacts,
        int Opportunities,
        int Cases,
        int OpenCases,
        int Activities);
}
