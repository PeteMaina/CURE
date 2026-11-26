using CURE.Application.CoreCrm;
using CURE.Infrastructure.Persistence;
using Microsoft.Extensions.Options;
using Npgsql;
using Xunit;

namespace CURE.Integration.Tests;

public sealed class PostgreSqlFactAttribute : FactAttribute
{
    public PostgreSqlFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("CURE_TEST_POSTGRES_ADMIN_CONNECTION"))
            || string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("CURE_TEST_POSTGRES_APP_CONNECTION")))
        {
            Skip = "Set both CURE_TEST_POSTGRES_ADMIN_CONNECTION and CURE_TEST_POSTGRES_APP_CONNECTION to run PostgreSQL security tests.";
        }
    }
}

public sealed class PostgresTenantIsolationTests
{
    [PostgreSqlFact]
    public async Task Runtime_role_is_non_privileged_and_cannot_read_or_write_across_tenants()
    {
        var adminConnectionString = Environment.GetEnvironmentVariable("CURE_TEST_POSTGRES_ADMIN_CONNECTION")!;
        var appConnectionString = Environment.GetEnvironmentVariable("CURE_TEST_POSTGRES_APP_CONNECTION")!;
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        var customerA = Guid.NewGuid();
        var customerB = Guid.NewGuid();

        await using var admin = new NpgsqlConnection(adminConnectionString);
        await admin.OpenAsync();
        await using var app = new NpgsqlConnection(appConnectionString);
        await app.OpenAsync();

        try
        {
            await using (var seedTenants = new NpgsqlCommand(
                """
                INSERT INTO tenants (id, name, slug) VALUES
                    (@TenantA, 'Isolation test A', @SlugA),
                    (@TenantB, 'Isolation test B', @SlugB);
                """,
                admin))
            {
                seedTenants.Parameters.AddWithValue("TenantA", tenantA);
                seedTenants.Parameters.AddWithValue("TenantB", tenantB);
                seedTenants.Parameters.AddWithValue("SlugA", $"isolation-{tenantA:N}");
                seedTenants.Parameters.AddWithValue("SlugB", $"isolation-{tenantB:N}");
                await seedTenants.ExecuteNonQueryAsync();
            }

            await using (var seedCustomers = new NpgsqlCommand(
                """
                INSERT INTO customers (id, tenant_id, name, normalized_name, customer_type)
                VALUES
                    (@CustomerA, @TenantA, 'Isolation A', 'isolation a', 'Business'),
                    (@CustomerB, @TenantB, 'Isolation B', 'isolation b', 'Business');
                """,
                admin))
            {
                seedCustomers.Parameters.AddWithValue("TenantA", tenantA);
                seedCustomers.Parameters.AddWithValue("TenantB", tenantB);
                seedCustomers.Parameters.AddWithValue("CustomerA", customerA);
                seedCustomers.Parameters.AddWithValue("CustomerB", customerB);
                await seedCustomers.ExecuteNonQueryAsync();
            }

            await using (var roleCheck = new NpgsqlCommand(
                "SELECT rolsuper OR rolbypassrls FROM pg_roles WHERE rolname = current_user;",
                app))
            {
                Assert.False((bool)(await roleCheck.ExecuteScalarAsync())!);
            }

            await using (var setTenant = new NpgsqlCommand(
                "SELECT set_config('app.tenant_id', @TenantId, false);",
                app))
            {
                setTenant.Parameters.AddWithValue("TenantId", tenantA.ToString());
                await setTenant.ExecuteNonQueryAsync();
            }

            await using (var read = new NpgsqlCommand(
                "SELECT count(*) FROM customers WHERE id = ANY(@CustomerIds);",
                app))
            {
                read.Parameters.AddWithValue("CustomerIds", new[] { customerA, customerB });
                Assert.Equal(1L, (long)(await read.ExecuteScalarAsync())!);
            }

            await using var crossTenantWrite = new NpgsqlCommand(
                """
                INSERT INTO customers (id, tenant_id, name, normalized_name, customer_type)
                VALUES (@Id, @TenantB, 'Forbidden', 'forbidden', 'Business');
                """,
                app);
            crossTenantWrite.Parameters.AddWithValue("Id", Guid.NewGuid());
            crossTenantWrite.Parameters.AddWithValue("TenantB", tenantB);
            var exception = await Assert.ThrowsAsync<PostgresException>(
                () => crossTenantWrite.ExecuteNonQueryAsync());
            Assert.Equal("42501", exception.SqlState);
        }
        finally
        {
            await using var cleanupCustomers = new NpgsqlCommand(
                "DELETE FROM customers WHERE id = ANY(@CustomerIds);",
                admin);
            cleanupCustomers.Parameters.AddWithValue("CustomerIds", new[] { customerA, customerB });
            await cleanupCustomers.ExecuteNonQueryAsync();
            await using var cleanupTenants = new NpgsqlCommand(
                "DELETE FROM tenants WHERE id = ANY(@TenantIds);",
                admin);
            cleanupTenants.Parameters.AddWithValue("TenantIds", new[] { tenantA, tenantB });
            await cleanupTenants.ExecuteNonQueryAsync();
        }
    }

    [PostgreSqlFact]
    public async Task Relational_foreign_keys_reject_cross_tenant_references()
    {
        var adminConnectionString = Environment.GetEnvironmentVariable("CURE_TEST_POSTGRES_ADMIN_CONNECTION")!;
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        var customerB = Guid.NewGuid();

        await using var admin = new NpgsqlConnection(adminConnectionString);
        await admin.OpenAsync();
        try
        {
            await using (var seedTenants = new NpgsqlCommand(
                """
                INSERT INTO tenants (id, name, slug) VALUES
                    (@TenantA, 'FK test A', @SlugA),
                    (@TenantB, 'FK test B', @SlugB);
                """,
                admin))
            {
                seedTenants.Parameters.AddWithValue("TenantA", tenantA);
                seedTenants.Parameters.AddWithValue("TenantB", tenantB);
                seedTenants.Parameters.AddWithValue("SlugA", $"foreign-key-{tenantA:N}");
                seedTenants.Parameters.AddWithValue("SlugB", $"foreign-key-{tenantB:N}");
                await seedTenants.ExecuteNonQueryAsync();
            }

            await using (var seedCustomer = new NpgsqlCommand(
                """
                INSERT INTO customers (id, tenant_id, name, normalized_name, customer_type)
                VALUES (@CustomerB, @TenantB, 'Foreign customer', 'foreign customer', 'Business');
                """,
                admin))
            {
                seedCustomer.Parameters.AddWithValue("TenantB", tenantB);
                seedCustomer.Parameters.AddWithValue("CustomerB", customerB);
                await seedCustomer.ExecuteNonQueryAsync();
            }

            await using var invalidReference = new NpgsqlCommand(
                """
                INSERT INTO opportunities
                    (id, tenant_id, opportunity_number, customer_id, name, stage, amount, currency, probability)
                VALUES
                    (@Id, @TenantA, @Number, @CustomerB, 'Cross-tenant reference', 'QUALIFICATION', 1, 'USD', 0);
                """,
                admin);
            invalidReference.Parameters.AddWithValue("Id", Guid.NewGuid());
            invalidReference.Parameters.AddWithValue("TenantA", tenantA);
            invalidReference.Parameters.AddWithValue("Number", $"OPP-{Guid.NewGuid():N}");
            invalidReference.Parameters.AddWithValue("CustomerB", customerB);
            var exception = await Assert.ThrowsAsync<PostgresException>(
                () => invalidReference.ExecuteNonQueryAsync());
            Assert.Equal("23503", exception.SqlState);
        }
        finally
        {
            await using var cleanupCustomer = new NpgsqlCommand(
                "DELETE FROM customers WHERE id = @CustomerB;",
                admin);
            cleanupCustomer.Parameters.AddWithValue("CustomerB", customerB);
            await cleanupCustomer.ExecuteNonQueryAsync();
            await using var cleanupTenants = new NpgsqlCommand(
                "DELETE FROM tenants WHERE id = ANY(@TenantIds);",
                admin);
            cleanupTenants.Parameters.AddWithValue("TenantIds", new[] { tenantA, tenantB });
            await cleanupTenants.ExecuteNonQueryAsync();
        }
    }

    [PostgreSqlFact]
    public async Task Core_record_mutation_and_audit_are_committed_together()
    {
        var adminConnectionString = Environment.GetEnvironmentVariable("CURE_TEST_POSTGRES_ADMIN_CONNECTION")!;
        var appConnectionString = Environment.GetEnvironmentVariable("CURE_TEST_POSTGRES_APP_CONNECTION")!;
        var tenantId = Guid.NewGuid();
        var actorId = Guid.NewGuid();
        var slug = $"audit-{tenantId:N}";
        var organizationId = Guid.Empty;

        await using var admin = new NpgsqlConnection(adminConnectionString);
        await admin.OpenAsync();
        try
        {
            await using (var seedTenant = new NpgsqlCommand(
                "INSERT INTO tenants (id, name, slug) VALUES (@Id, 'Audit test', @Slug);",
                admin))
            {
                seedTenant.Parameters.AddWithValue("Id", tenantId);
                seedTenant.Parameters.AddWithValue("Slug", slug);
                await seedTenant.ExecuteNonQueryAsync();
            }

            var factory = new PostgresConnectionFactory(Options.Create(new DatabaseOptions
            {
                ConnectionString = appConnectionString,
            }));
            var store = new PostgresCoreCrmStore(factory);
            var created = await store.CreateOrganizationAsync(
                tenantId,
                actorId,
                "audit-test-create",
                new OrganizationCommand("Audit atomicity", null, null),
                CancellationToken.None);
            organizationId = created.Id;

            await using (var readAudit = new NpgsqlCommand(
                """
                SELECT count(*) FROM audit_events
                WHERE tenant_id = @TenantId AND entity_id = @EntityId
                  AND action = 'ORGANIZATION_CREATED'
                  AND after_state ->> 'name' = 'Audit atomicity';
                """,
                admin))
            {
                readAudit.Parameters.AddWithValue("TenantId", tenantId);
                readAudit.Parameters.AddWithValue("EntityId", organizationId);
                Assert.Equal(1L, (long)(await readAudit.ExecuteScalarAsync())!);
            }

            var staleUpdate = await store.UpdateOrganizationAsync(
                tenantId,
                actorId,
                "audit-test-stale-update",
                organizationId,
                new OrganizationCommand("Should not change", null, null, ExpectedVersion: 0),
                CancellationToken.None);
            Assert.Null(staleUpdate);

            await using (var verifyNoStaleAudit = new NpgsqlCommand(
                "SELECT count(*) FROM audit_events WHERE tenant_id = @TenantId AND entity_id = @EntityId;",
                admin))
            {
                verifyNoStaleAudit.Parameters.AddWithValue("TenantId", tenantId);
                verifyNoStaleAudit.Parameters.AddWithValue("EntityId", organizationId);
                Assert.Equal(1L, (long)(await verifyNoStaleAudit.ExecuteScalarAsync())!);
            }
        }
        finally
        {
            if (organizationId != Guid.Empty)
            {
                await using var cleanupAudit = new NpgsqlCommand(
                    "DELETE FROM audit_events WHERE tenant_id = @TenantId AND entity_id = @EntityId;",
                    admin);
                cleanupAudit.Parameters.AddWithValue("TenantId", tenantId);
                cleanupAudit.Parameters.AddWithValue("EntityId", organizationId);
                await cleanupAudit.ExecuteNonQueryAsync();
                await using var cleanupOrganization = new NpgsqlCommand(
                    "DELETE FROM organizations WHERE tenant_id = @TenantId AND id = @EntityId;",
                    admin);
                cleanupOrganization.Parameters.AddWithValue("TenantId", tenantId);
                cleanupOrganization.Parameters.AddWithValue("EntityId", organizationId);
                await cleanupOrganization.ExecuteNonQueryAsync();
            }

            await using var cleanupTenant = new NpgsqlCommand(
                "DELETE FROM tenants WHERE id = @TenantId;",
                admin);
            cleanupTenant.Parameters.AddWithValue("TenantId", tenantId);
            await cleanupTenant.ExecuteNonQueryAsync();
        }
    }
}
