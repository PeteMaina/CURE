using Dapper;
using Microsoft.Extensions.Options;
using Npgsql;

namespace CURE.Infrastructure.Persistence;

public sealed class PostgresConnectionFactory(IOptions<DatabaseOptions> options)
{
    private readonly DatabaseOptions settings = options.Value;

    public async Task<NpgsqlConnection> OpenAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(settings.ConnectionString))
        {
            throw new InvalidOperationException("Database:ConnectionString must identify the non-owner application role.");
        }

        var connection = new NpgsqlConnection(settings.ConnectionString);
        await connection.OpenAsync(cancellationToken);
        return connection;
    }

    public async Task<NpgsqlConnection> OpenMigrationAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(settings.MigrationConnectionString))
        {
            throw new InvalidOperationException("Database:MigrationConnectionString must be configured for schema initialization.");
        }

        var connection = new NpgsqlConnection(settings.MigrationConnectionString);
        await connection.OpenAsync(cancellationToken);
        return connection;
    }

    public async Task EnsureDevelopmentRuntimeRoleAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(settings.RuntimeRoleName)
            || string.IsNullOrWhiteSpace(settings.RuntimeRolePassword))
        {
            throw new InvalidOperationException(
                "Development requires Database:RuntimeRoleName and Database:RuntimeRolePassword.");
        }

        await using var connection = await OpenMigrationAsync(cancellationToken);
        var roleSql = await connection.ExecuteScalarAsync<string>(
            """
            SELECT CASE
                WHEN EXISTS (SELECT 1 FROM pg_roles WHERE rolname = @RoleName)
                    THEN format('ALTER ROLE %I WITH LOGIN PASSWORD %L NOSUPERUSER NOCREATEDB NOCREATEROLE NOINHERIT NOBYPASSRLS', @RoleName, @Password)
                ELSE format('CREATE ROLE %I WITH LOGIN PASSWORD %L NOSUPERUSER NOCREATEDB NOCREATEROLE NOINHERIT NOBYPASSRLS', @RoleName, @Password)
            END;
            """,
            new { RoleName = settings.RuntimeRoleName, Password = settings.RuntimeRolePassword });
        await connection.ExecuteAsync(roleSql);
    }

    public async Task<NpgsqlConnection> OpenForTenantAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT set_config('app.tenant_id', $1, false);";
        command.Parameters.AddWithValue(tenantId.ToString());
        await command.ExecuteNonQueryAsync(cancellationToken);
        return connection;
    }
}