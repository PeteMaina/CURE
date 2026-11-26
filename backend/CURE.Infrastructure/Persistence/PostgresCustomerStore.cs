using System.Text.Json;
using CURE.Application.Customers;
using Dapper;
using Npgsql;

namespace CURE.Infrastructure.Persistence;

public sealed class PostgresCustomerStore(PostgresConnectionFactory connectionFactory) : ICustomerStore
{
    public async Task<CustomerPage> ListAsync(Guid tenantId, CustomerListQuery query, CancellationToken cancellationToken)
    {
        await using var connection = await connectionFactory.OpenForTenantAsync(tenantId, cancellationToken);
        var offset = (query.Page - 1) * query.PageSize;
        var search = string.IsNullOrWhiteSpace(query.Search) ? null : $"%{query.Search.Trim().ToLowerInvariant()}%";
        const string sql = """
            SELECT c.id AS Id, c.name AS Name, c.customer_type AS Type, c.industry AS Industry,
                   c.owner_id AS OwnerId, u.display_name AS OwnerName, c.updated_at AS UpdatedAt, c.version AS Version
            FROM customers c LEFT JOIN users u ON u.id = c.owner_id AND u.tenant_id = c.tenant_id
            WHERE c.tenant_id = @TenantId
              AND (@Search IS NULL OR c.normalized_name LIKE @Search OR c.normalized_email LIKE @Search)
              AND (@Type IS NULL OR lower(c.customer_type) = lower(@Type))
              AND (@Industry IS NULL OR lower(c.industry) = lower(@Industry))
            ORDER BY c.updated_at DESC
            OFFSET @Offset LIMIT @PageSize;
            SELECT COUNT(*) FROM customers c
            WHERE c.tenant_id = @TenantId
              AND (@Search IS NULL OR c.normalized_name LIKE @Search OR c.normalized_email LIKE @Search)
              AND (@Type IS NULL OR lower(c.customer_type) = lower(@Type))
              AND (@Industry IS NULL OR lower(c.industry) = lower(@Industry));
            """;
        using var grid = await connection.QueryMultipleAsync(new CommandDefinition(sql, new
        {
            TenantId = tenantId,
            Search = search,
            query.Type,
            query.Industry,
            Offset = offset,
            query.PageSize
        }, cancellationToken: cancellationToken));
        var rows = (await grid.ReadAsync<CustomerListItem>()).AsList();
        var total = await grid.ReadSingleAsync<int>();
        return new CustomerPage(rows, query.Page, query.PageSize, total);
    }

    public async Task<CustomerDetail?> GetAsync(Guid tenantId, Guid customerId, CancellationToken cancellationToken)
    {
        await using var connection = await connectionFactory.OpenForTenantAsync(tenantId, cancellationToken);
        const string sql = """
            SELECT id AS Id, name AS Name, customer_type AS Type, industry AS Industry, email AS Email,
                   phone AS Phone, owner_id AS OwnerId, created_at AS CreatedAt, updated_at AS UpdatedAt, version AS Version
            FROM customers WHERE tenant_id = @TenantId AND id = @CustomerId;
            """;
        return await connection.QuerySingleOrDefaultAsync<CustomerDetail>(new CommandDefinition(sql, new { TenantId = tenantId, CustomerId = customerId }, cancellationToken: cancellationToken));
    }

    public async Task<CustomerDetail?> FindDuplicateAsync(Guid tenantId, string? normalizedEmail, string? normalizedPhone, string normalizedName, CancellationToken cancellationToken)
    {
        await using var connection = await connectionFactory.OpenForTenantAsync(tenantId, cancellationToken);
        const string sql = """
            SELECT id AS Id, name AS Name, customer_type AS Type, industry AS Industry, email AS Email,
                   phone AS Phone, owner_id AS OwnerId, created_at AS CreatedAt, updated_at AS UpdatedAt, version AS Version
            FROM customers
            WHERE tenant_id = @TenantId AND (normalized_name = @NormalizedName OR (@NormalizedEmail IS NOT NULL AND normalized_email = @NormalizedEmail) OR (@NormalizedPhone IS NOT NULL AND normalized_phone = @NormalizedPhone))
            LIMIT 1;
            """;
        return await connection.QuerySingleOrDefaultAsync<CustomerDetail>(new CommandDefinition(sql, new { TenantId = tenantId, NormalizedName = normalizedName, NormalizedEmail = normalizedEmail, NormalizedPhone = normalizedPhone }, cancellationToken: cancellationToken));
    }

    public async Task<CustomerDetail> CreateAsync(Guid tenantId, Guid actorId, string requestId, CreateCustomerCommand command, CancellationToken cancellationToken)
    {
        await using var connection = await connectionFactory.OpenForTenantAsync(tenantId, cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        const string sql = """
            INSERT INTO customers (id, tenant_id, name, normalized_name, customer_type, industry, email, normalized_email, phone, normalized_phone, owner_id)
            VALUES (@Id, @TenantId, @Name, @NormalizedName, @Type, @Industry, @Email, @NormalizedEmail, @Phone, @NormalizedPhone, @OwnerId);
            """;
        var id = Guid.CreateVersion7();
        await connection.ExecuteAsync(new CommandDefinition(sql, new { Id = id, TenantId = tenantId, Name = command.Name.Trim(), NormalizedName = command.Name.Trim().ToLowerInvariant(), Type = command.Type, command.Industry, command.Email, NormalizedEmail = command.Email?.Trim().ToLowerInvariant(), command.Phone, NormalizedPhone = command.Phone is null ? null : new string(command.Phone.Where(char.IsAsciiDigit).ToArray()), command.OwnerId }, transaction, cancellationToken: cancellationToken));
        var created = await QueryDetailAsync(connection, transaction, tenantId, id, cancellationToken);
        await WriteAuditAsync(connection, transaction, tenantId, actorId, requestId, "CUSTOMER_CREATED", id, null, created, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return created;
    }

    public async Task<CustomerDetail?> UpdateAsync(Guid tenantId, Guid actorId, string requestId, Guid customerId, UpdateCustomerCommand command, CancellationToken cancellationToken)
    {
        await using var connection = await connectionFactory.OpenForTenantAsync(tenantId, cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        var before = await QueryDetailAsync(connection, transaction, tenantId, customerId, cancellationToken, forUpdate: true);
        if (before is null)
        {
            await transaction.CommitAsync(cancellationToken);
            return null;
        }

        const string sql = """
            UPDATE customers SET name = @Name, normalized_name = @NormalizedName, customer_type = @Type, industry = @Industry,
                email = @Email, normalized_email = @NormalizedEmail, phone = @Phone, normalized_phone = @NormalizedPhone,
                owner_id = @OwnerId, updated_at = now(), version = version + 1
            WHERE tenant_id = @TenantId AND id = @CustomerId AND version = @ExpectedVersion;
            """;
        var changed = await connection.ExecuteAsync(new CommandDefinition(sql, new { TenantId = tenantId, CustomerId = customerId, command.ExpectedVersion, Name = command.Name.Trim(), NormalizedName = command.Name.Trim().ToLowerInvariant(), command.Type, command.Industry, command.Email, NormalizedEmail = command.Email?.Trim().ToLowerInvariant(), command.Phone, NormalizedPhone = command.Phone is null ? null : new string(command.Phone.Where(char.IsAsciiDigit).ToArray()), command.OwnerId }, transaction, cancellationToken: cancellationToken));
        if (changed == 0)
        {
            await transaction.CommitAsync(cancellationToken);
            return null;
        }

        var updated = await QueryDetailAsync(connection, transaction, tenantId, customerId, cancellationToken);
        await WriteAuditAsync(connection, transaction, tenantId, actorId, requestId, "CUSTOMER_UPDATED", customerId, before, updated, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return updated;
    }

    public async Task<bool> DeleteAsync(Guid tenantId, Guid actorId, string requestId, Guid customerId, CancellationToken cancellationToken)
    {
        await using var connection = await connectionFactory.OpenForTenantAsync(tenantId, cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        var before = await QueryDetailAsync(connection, transaction, tenantId, customerId, cancellationToken, forUpdate: true);
        if (before is null)
        {
            await transaction.CommitAsync(cancellationToken);
            return false;
        }

        await connection.ExecuteAsync(new CommandDefinition("DELETE FROM customers WHERE tenant_id = @TenantId AND id = @CustomerId", new { TenantId = tenantId, CustomerId = customerId }, transaction, cancellationToken: cancellationToken));
        await WriteAuditAsync(connection, transaction, tenantId, actorId, requestId, "CUSTOMER_DELETED", customerId, before, null, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return true;
    }

    private static async Task<CustomerDetail?> QueryDetailAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid tenantId,
        Guid customerId,
        CancellationToken cancellationToken,
        bool forUpdate = false)
    {
        var sql = """
            SELECT id AS Id, name AS Name, customer_type AS Type, industry AS Industry, email AS Email,
                   phone AS Phone, owner_id AS OwnerId, created_at AS CreatedAt, updated_at AS UpdatedAt, version AS Version
            FROM customers WHERE tenant_id = @TenantId AND id = @CustomerId
            """ + (forUpdate ? " FOR UPDATE;" : ";");
        return await connection.QuerySingleOrDefaultAsync<CustomerDetail>(new CommandDefinition(
            sql, new { TenantId = tenantId, CustomerId = customerId }, transaction, cancellationToken: cancellationToken));
    }

    private static async Task WriteAuditAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid tenantId,
        Guid actorId,
        string requestId,
        string action,
        Guid entityId,
        object? before,
        object? after,
        CancellationToken cancellationToken)
    {
        const string sql = "INSERT INTO audit_events (id, tenant_id, actor_id, action, entity_type, entity_id, request_id, before_state, after_state) VALUES (@Id, @TenantId, @ActorId, @Action, 'CUSTOMER', @EntityId, @RequestId, CAST(@Before AS jsonb), CAST(@After AS jsonb));";
        await connection.ExecuteAsync(new CommandDefinition(sql, new { Id = Guid.CreateVersion7(), TenantId = tenantId, ActorId = actorId, Action = action, EntityId = entityId, RequestId = requestId, Before = before is null ? null : JsonSerializer.Serialize(before), After = after is null ? null : JsonSerializer.Serialize(after) }, transaction, cancellationToken: cancellationToken));
    }
}