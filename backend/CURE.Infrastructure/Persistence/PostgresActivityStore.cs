using System.Data;
using CURE.Application.Engagement;
using CURE.Domain.Engagement;
using CURE.Domain.Shared;
using Dapper;

namespace CURE.Infrastructure.Persistence;

/// <summary>
/// Postgres-backed implementation of <see cref="IActivityStore"/>.
///
/// Every write happens inside a transaction that:
///   1. Loads the activity aggregate and enforces the domain invariants.
///   2. Persists the mutated row with an optimistic-lock version bump.
///   3. Appends a row to <c>audit_events</c> in the same transaction,
///      guaranteeing the audit trail and the data row are always in sync.
///
/// Timeline reads hit <c>customer_timeline_view</c> — a SQL UNION view that
/// merges activities, opportunity stage history, and audit events into a
/// single keyset-paginated cursor ordered newest-first.
/// </summary>
public sealed class PostgresActivityStore(PostgresConnectionFactory connectionFactory) : IActivityStore
{
    // ── Log ───────────────────────────────────────────────────────────────────

    public async Task<ActivityDto> LogActivityAsync(
        Guid tenantId,
        Guid actorId,
        string requestId,
        LogActivityCommand command,
        CancellationToken ct)
    {
        await using var conn = await connectionFactory.OpenAppAsync(tenantId, ct);
        await using var tx   = await conn.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);

        // Idempotency guard — same requestId returns the already-persisted row.
        var existing = await conn.QuerySingleOrDefaultAsync<ActivityDto>(
            new CommandDefinition(
                """
                SELECT a.id AS Id, a.tenant_id AS TenantId, a.activity_type AS Type,
                       a.status AS Status, a.subject AS Subject, a.notes AS Notes,
                       a.scheduled_at AS ScheduledAt, a.completed_at AS CompletedAt,
                       a.duration_minutes AS DurationMinutes, a.outcome AS Outcome,
                       a.linked_customer_id AS LinkedCustomerId,
                       a.linked_contact_id AS LinkedContactId,
                       a.linked_opportunity_id AS LinkedOpportunityId,
                       a.owner_id AS OwnerId,
                       a.created_at AS CreatedAt, a.updated_at AS UpdatedAt,
                       a.version AS Version
                FROM activities a
                WHERE a.tenant_id = @TenantId AND a.idempotency_key = @RequestId
                LIMIT 1;
                """,
                new { TenantId = tenantId, RequestId = requestId },
                transaction: tx, cancellationToken: ct));

        if (existing is not null)
        {
            await tx.RollbackAsync(ct);
            return existing;
        }

        var domainType = (ActivityType)(int)command.Type;
        var activity   = Activity.Log(
            tenantId:            tenantId,
            type:                domainType,
            subject:             command.Subject,
            notes:               command.Notes,
            scheduledAt:         command.ScheduledAt,
            durationMinutes:     command.DurationMinutes,
            linkedCustomerId:    command.LinkedCustomerId,
            linkedContactId:     command.LinkedContactId,
            linkedOpportunityId: command.LinkedOpportunityId,
            ownerId:             actorId,
            actorId:             actorId,
            now:                 DateTimeOffset.UtcNow);

        const string insertSql =
            """
            INSERT INTO activities (
                id, tenant_id, idempotency_key,
                activity_type, status, subject, notes,
                scheduled_at, duration_minutes, outcome,
                linked_customer_id, linked_contact_id, linked_opportunity_id,
                owner_id, created_at, created_by, updated_at, updated_by, version)
            VALUES (
                @Id, @TenantId, @IdempotencyKey,
                @Type, @Status, @Subject, @Notes,
                @ScheduledAt, @DurationMinutes, NULL,
                @LinkedCustomerId, @LinkedContactId, @LinkedOpportunityId,
                @OwnerId, @CreatedAt, @CreatedBy, @UpdatedAt, @UpdatedBy, 1)
            RETURNING id;
            """;

        await conn.ExecuteAsync(new CommandDefinition(insertSql, new
        {
            Id                  = activity.Id,
            TenantId            = activity.TenantId,
            IdempotencyKey      = requestId,
            Type                = activity.Type.ToString(),
            Status              = activity.Status.ToString(),
            Subject             = activity.Subject,
            Notes               = activity.Notes,
            ScheduledAt         = activity.ScheduledAt,
            DurationMinutes     = activity.DurationMinutes,
            LinkedCustomerId    = activity.LinkedCustomerId,
            LinkedContactId     = activity.LinkedContactId,
            LinkedOpportunityId = activity.LinkedOpportunityId,
            OwnerId             = activity.OwnerId,
            CreatedAt           = activity.CreatedAt,
            CreatedBy           = activity.CreatedBy,
            UpdatedAt           = activity.UpdatedAt,
            UpdatedBy           = activity.UpdatedBy,
        }, transaction: tx, cancellationToken: ct));

        await AppendAuditEventAsync(conn, tx, activity.DomainEvents[0], actorId, ct);
        await tx.CommitAsync(ct);

        return await FetchByIdAsync(conn, tenantId, activity.Id, null, ct);
    }

    // ── Complete ──────────────────────────────────────────────────────────────

    public async Task<ActivityDto> CompleteActivityAsync(
        Guid tenantId,
        Guid actorId,
        string requestId,
        Guid activityId,
        CompleteActivityCommand command,
        CancellationToken ct)
    {
        await using var conn = await connectionFactory.OpenAppAsync(tenantId, ct);
        await using var tx   = await conn.BeginTransactionAsync(IsolationLevel.RepeatableRead, ct);

        var activity = await LoadAsync(conn, tx, tenantId, activityId, ct);
        activity.AssertBelongsToTenant(tenantId);

        if (command.ExpectedVersion.HasValue && activity.Version != command.ExpectedVersion.Value)
        {
            throw new ConcurrencyException("Activity", command.ExpectedVersion.Value, activity.Version);
        }

        var now = DateTimeOffset.UtcNow;
        activity.Complete(command.Outcome, command.ActualDurationMinutes, command.ClosingNotes, actorId, now);

        const string updateSql =
            """
            UPDATE activities
            SET status = @Status, outcome = @Outcome, completed_at = @CompletedAt,
                duration_minutes = @DurationMinutes, notes = @Notes,
                updated_at = @UpdatedAt, updated_by = @UpdatedBy,
                version = version + 1
            WHERE id = @Id AND tenant_id = @TenantId AND version = @Version;
            """;

        var rows = await conn.ExecuteAsync(new CommandDefinition(updateSql, new
        {
            Id              = activity.Id,
            TenantId        = tenantId,
            Version         = activity.Version,
            Status          = activity.Status.ToString(),
            Outcome         = activity.Outcome,
            CompletedAt     = activity.CompletedAt,
            DurationMinutes = activity.DurationMinutes,
            Notes           = activity.Notes,
            UpdatedAt       = now,
            UpdatedBy       = actorId,
        }, transaction: tx, cancellationToken: ct));

        if (rows == 0)
        {
            throw new ConcurrencyException("Activity", activity.Version, null);
        }

        await AppendAuditEventAsync(conn, tx, activity.DomainEvents[0], actorId, ct);
        await tx.CommitAsync(ct);

        return await FetchByIdAsync(conn, tenantId, activityId, null, ct);
    }

    // ── Cancel ────────────────────────────────────────────────────────────────

    public async Task<ActivityDto> CancelActivityAsync(
        Guid tenantId,
        Guid actorId,
        Guid activityId,
        CancelActivityCommand command,
        CancellationToken ct)
    {
        await using var conn = await connectionFactory.OpenAppAsync(tenantId, ct);
        await using var tx   = await conn.BeginTransactionAsync(IsolationLevel.RepeatableRead, ct);

        var activity = await LoadAsync(conn, tx, tenantId, activityId, ct);
        activity.AssertBelongsToTenant(tenantId);

        if (command.ExpectedVersion.HasValue && activity.Version != command.ExpectedVersion.Value)
        {
            throw new ConcurrencyException("Activity", command.ExpectedVersion.Value, activity.Version);
        }

        var now = DateTimeOffset.UtcNow;
        activity.Cancel(command.Reason, actorId, now);

        const string updateSql =
            """
            UPDATE activities
            SET status = @Status, notes = @Notes,
                updated_at = @UpdatedAt, updated_by = @UpdatedBy,
                version = version + 1
            WHERE id = @Id AND tenant_id = @TenantId AND version = @Version;
            """;

        var rows = await conn.ExecuteAsync(new CommandDefinition(updateSql, new
        {
            Id       = activity.Id,
            TenantId = tenantId,
            Version  = activity.Version,
            Status   = activity.Status.ToString(),
            Notes    = activity.Notes,
            UpdatedAt = now,
            UpdatedBy = actorId,
        }, transaction: tx, cancellationToken: ct));

        if (rows == 0)
        {
            throw new ConcurrencyException("Activity", activity.Version, null);
        }

        await AppendAuditEventAsync(conn, tx, activity.DomainEvents[0], actorId, ct);
        await tx.CommitAsync(ct);

        return await FetchByIdAsync(conn, tenantId, activityId, null, ct);
    }

    // ── Unified customer timeline ─────────────────────────────────────────────

    public async Task<CustomerTimelineDto> GetCustomerTimelineAsync(
        Guid tenantId,
        Guid customerId,
        int limit,
        DateTimeOffset? before,
        CancellationToken ct)
    {
        await using var conn = await connectionFactory.OpenAppAsync(tenantId, ct);

        const string sql =
            """
            SELECT
                entry_id         AS EntryId,
                entry_kind       AS EntryKind,
                event_type       AS EventType,
                summary          AS Summary,
                detail           AS Detail,
                linked_opportunity_id AS LinkedOpportunityId,
                linked_activity_id    AS LinkedActivityId,
                actor_id         AS ActorId,
                occurred_at      AS OccurredAt
            FROM customer_timeline_view
            WHERE tenant_id   = @TenantId
              AND customer_id = @CustomerId
              AND (@Before::timestamptz IS NULL OR occurred_at < @Before)
            ORDER BY occurred_at DESC
            LIMIT @Limit;
            """;

        var rows = (await conn.QueryAsync<TimelineEntryDto>(
            new CommandDefinition(sql,
                new { TenantId = tenantId, CustomerId = customerId, Before = before, Limit = limit + 1 },
                cancellationToken: ct))).ToList();

        var hasMore = rows.Count > limit;
        var entries = (hasMore ? rows.Take(limit) : rows).ToList();

        return new CustomerTimelineDto(
            CustomerId:    customerId,
            Entries:       entries,
            HasMore:       hasMore,
            OldestEntryAt: entries.Count > 0 ? entries[^1].OccurredAt : null);
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    private static async Task<Activity> LoadAsync(
        System.Data.Common.DbConnection conn,
        System.Data.Common.DbTransaction tx,
        Guid tenantId,
        Guid activityId,
        CancellationToken ct)
    {
        const string sql =
            """
            SELECT id, tenant_id, activity_type, status, subject, notes,
                   scheduled_at, completed_at, duration_minutes, outcome,
                   linked_customer_id, linked_contact_id, linked_opportunity_id,
                   owner_id, created_at, created_by, updated_at, updated_by, version
            FROM activities
            WHERE id = @Id AND tenant_id = @TenantId
            FOR UPDATE;
            """;

        var row = await conn.QuerySingleOrDefaultAsync<dynamic>(
            new CommandDefinition(sql, new { Id = activityId, TenantId = tenantId },
                transaction: tx, cancellationToken: ct));

        if (row is null)
        {
            throw DomainException.NotFound("Activity", activityId);
        }

        return Activity.Reconstitute(
            id:                  (Guid)row.id,
            tenantId:            (Guid)row.tenant_id,
            type:                Enum.Parse<ActivityType>((string)row.activity_type),
            status:              Enum.Parse<ActivityStatus>((string)row.status),
            subject:             (string)row.subject,
            notes:               (string?)row.notes,
            scheduledAt:         (DateTimeOffset?)row.scheduled_at,
            completedAt:         (DateTimeOffset?)row.completed_at,
            durationMinutes:     (int?)row.duration_minutes,
            outcome:             (string?)row.outcome,
            linkedCustomerId:    (Guid?)row.linked_customer_id,
            linkedContactId:     (Guid?)row.linked_contact_id,
            linkedOpportunityId: (Guid?)row.linked_opportunity_id,
            ownerId:             (Guid)row.owner_id,
            createdAt:           (DateTimeOffset)row.created_at,
            createdBy:           (Guid)row.created_by,
            updatedAt:           (DateTimeOffset)row.updated_at,
            updatedBy:           (Guid)row.updated_by,
            version:             (int)row.version);
    }

    private static async Task<ActivityDto> FetchByIdAsync(
        System.Data.Common.DbConnection conn,
        Guid tenantId,
        Guid activityId,
        System.Data.Common.DbTransaction? tx,
        CancellationToken ct)
    {
        const string sql =
            """
            SELECT id AS Id, tenant_id AS TenantId, activity_type AS Type,
                   status AS Status, subject AS Subject, notes AS Notes,
                   scheduled_at AS ScheduledAt, completed_at AS CompletedAt,
                   duration_minutes AS DurationMinutes, outcome AS Outcome,
                   linked_customer_id AS LinkedCustomerId,
                   linked_contact_id AS LinkedContactId,
                   linked_opportunity_id AS LinkedOpportunityId,
                   owner_id AS OwnerId,
                   created_at AS CreatedAt, updated_at AS UpdatedAt,
                   version AS Version
            FROM activities
            WHERE id = @Id AND tenant_id = @TenantId;
            """;

        return await conn.QuerySingleAsync<ActivityDto>(
            new CommandDefinition(sql, new { Id = activityId, TenantId = tenantId },
                transaction: tx, cancellationToken: ct));
    }

    private static async Task AppendAuditEventAsync(
        System.Data.Common.DbConnection conn,
        System.Data.Common.DbTransaction tx,
        CURE.Domain.Shared.DomainEvent domainEvent,
        Guid actorId,
        CancellationToken ct)
    {
        const string sql =
            """
            INSERT INTO audit_events (
                id, tenant_id, aggregate_type, aggregate_id, event_type,
                customer_id, actor_id, occurred_at, payload)
            VALUES (
                @Id, @TenantId, @AggregateType, @AggregateId, @EventType,
                @CustomerId, @ActorId, @OccurredAt,
                @Payload::jsonb);
            """;

        await conn.ExecuteAsync(new CommandDefinition(sql, new
        {
            Id            = domainEvent.EventId,
            TenantId      = domainEvent.TenantId,
            AggregateType = domainEvent.AggregateType,
            AggregateId   = domainEvent.AggregateId,
            EventType     = domainEvent.QualifiedName,
            CustomerId    = domainEvent.CustomerId,
            ActorId       = actorId,
            OccurredAt    = domainEvent.OccurredAt,
            Payload       = System.Text.Json.JsonSerializer.Serialize(domainEvent),
        }, transaction: tx, cancellationToken: ct));
    }
}
