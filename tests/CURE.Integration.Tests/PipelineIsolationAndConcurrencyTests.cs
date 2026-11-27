namespace CURE.Integration.Tests;

using CURE.Application.Pipeline;
using CURE.Domain.Pipeline;
using CURE.Domain.Shared;
using CURE.Infrastructure.Persistence;
using Microsoft.Extensions.Options;
using Npgsql;
using Xunit;

public sealed class PipelineIsolationAndConcurrencyTests
{
    [PostgreSqlFact]
    public async Task Pipeline_summary_and_stage_history_strictly_enforce_tenant_isolation()
    {
        var adminConnectionString = Environment.GetEnvironmentVariable("CURE_TEST_POSTGRES_ADMIN_CONNECTION")!;
        var appConnectionString = Environment.GetEnvironmentVariable("CURE_TEST_POSTGRES_APP_CONNECTION")!;
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        var oppA = Guid.NewGuid();
        var oppB = Guid.NewGuid();

        await using var admin = new NpgsqlConnection(adminConnectionString);
        await admin.OpenAsync();

        try
        {
            await using (var seedTenants = new NpgsqlCommand(
                """
                INSERT INTO tenants (id, name, slug) VALUES
                    (@TenantA, 'Pipeline Tenant A', @SlugA),
                    (@TenantB, 'Pipeline Tenant B', @SlugB);
                """,
                admin))
            {
                seedTenants.Parameters.AddWithValue("TenantA", tenantA);
                seedTenants.Parameters.AddWithValue("TenantB", tenantB);
                seedTenants.Parameters.AddWithValue("SlugA", $"pipe-a-{tenantA:N}");
                seedTenants.Parameters.AddWithValue("SlugB", $"pipe-b-{tenantB:N}");
                await seedTenants.ExecuteNonQueryAsync();
            }

            await using (var seedOpps = new NpgsqlCommand(
                """
                INSERT INTO opportunities (id, tenant_id, opportunity_number, name, stage, amount, currency, probability)
                VALUES
                    (@OppA, @TenantA, 'OPP-101', 'Tenant A Secret Deal', 'Proposal', 100000.00, 'USD', 50),
                    (@OppB, @TenantB, 'OPP-202', 'Tenant B Confidential Deal', 'Negotiation', 250000.00, 'USD', 75);

                INSERT INTO opportunity_stage_history (id, tenant_id, opportunity_id, from_stage, to_stage, amount, currency, probability, weighted_amount, changed_by)
                VALUES
                    (gen_random_uuid(), @TenantA, @OppA, 'Discovery', 'Proposal', 100000.00, 'USD', 50, 50000.00, gen_random_uuid()),
                    (gen_random_uuid(), @TenantB, @OppB, 'Proposal', 'Negotiation', 250000.00, 'USD', 75, 187500.00, gen_random_uuid());
                """,
                admin))
            {
                seedOpps.Parameters.AddWithValue("OppA", oppA);
                seedOpps.Parameters.AddWithValue("OppB", oppB);
                seedOpps.Parameters.AddWithValue("TenantA", tenantA);
                seedOpps.Parameters.AddWithValue("TenantB", tenantB);
                await seedOpps.ExecuteNonQueryAsync();
            }

            var factory = new PostgresConnectionFactory(Options.Create(new DatabaseOptions
            {
                ConnectionString = appConnectionString,
            }));
            var store = new PostgresPipelineStore(factory);

            // Tenant A queries pipeline summary
            var summaryA = await store.GetPipelineSummaryAsync(tenantA, CancellationToken.None);
            Assert.Equal(100_000.00m, summaryA.TotalPipelineAmount);
            Assert.Equal(50_000.00m, summaryA.TotalWeightedAmount);
            Assert.Equal(1, summaryA.TotalOpenDeals);

            // Tenant A queries stage history of its own opportunity
            var historyA = await store.GetStageHistoryAsync(tenantA, oppA, CancellationToken.None);
            Assert.Single(historyA);
            Assert.Equal("Proposal", historyA[0].ToStage);

            // Tenant A attempts to view Tenant B's stage history
            var crossTenantHistory = await store.GetStageHistoryAsync(tenantA, oppB, CancellationToken.None);
            Assert.Empty(crossTenantHistory);
        }
        finally
        {
            await using var cleanup = new NpgsqlCommand(
                """
                DELETE FROM opportunity_stage_history WHERE tenant_id IN (@TenantA, @TenantB);
                DELETE FROM opportunities WHERE tenant_id IN (@TenantA, @TenantB);
                DELETE FROM tenants WHERE id IN (@TenantA, @TenantB);
                """,
                admin);
            cleanup.Parameters.AddWithValue("TenantA", tenantA);
            cleanup.Parameters.AddWithValue("TenantB", tenantB);
            await cleanup.ExecuteNonQueryAsync();
        }
    }

    [PostgreSqlFact]
    public async Task Concurrent_stage_transitions_trigger_optimistic_concurrency_failure()
    {
        var adminConnectionString = Environment.GetEnvironmentVariable("CURE_TEST_POSTGRES_ADMIN_CONNECTION")!;
        var appConnectionString = Environment.GetEnvironmentVariable("CURE_TEST_POSTGRES_APP_CONNECTION")!;
        var tenantId = Guid.NewGuid();
        var oppId = Guid.NewGuid();
        var actorA = Guid.NewGuid();
        var actorB = Guid.NewGuid();

        await using var admin = new NpgsqlConnection(adminConnectionString);
        await admin.OpenAsync();

        try
        {
            await using (var seed = new NpgsqlCommand(
                """
                INSERT INTO tenants (id, name, slug) VALUES (@TenantId, 'Concurrency Tenant', @Slug);
                INSERT INTO opportunities (id, tenant_id, opportunity_number, name, stage, amount, currency, probability, version)
                VALUES (@OppId, @TenantId, 'OPP-999', 'Race Deal', 'Discovery', 50000.00, 'USD', 25, 1);
                """,
                admin))
            {
                seed.Parameters.AddWithValue("TenantId", tenantId);
                seed.Parameters.AddWithValue("OppId", oppId);
                seed.Parameters.AddWithValue("Slug", $"race-{tenantId:N}");
                await seed.ExecuteNonQueryAsync();
            }

            var factory = new PostgresConnectionFactory(Options.Create(new DatabaseOptions
            {
                ConnectionString = appConnectionString,
            }));
            var store = new PostgresPipelineStore(factory);

            // Worker A succeeds advancing version 1 -> 2
            var updateA = await store.TransitionStageAsync(
                tenantId, actorA, "req-worker-a", oppId,
                new TransitionStageCommand("Proposal", ExpectedVersion: 1),
                CancellationToken.None);

            Assert.Equal("Proposal", updateA.Stage);
            Assert.Equal(2, updateA.Version);

            // Worker B attempts to advance using stale version 1
            await Assert.ThrowsAsync<ConcurrencyException>(() =>
                store.TransitionStageAsync(
                    tenantId, actorB, "req-worker-b", oppId,
                    new TransitionStageCommand("Negotiation", ExpectedVersion: 1),
                    CancellationToken.None));
        }
        finally
        {
            await using var cleanup = new NpgsqlCommand(
                """
                DELETE FROM audit_events WHERE tenant_id = @TenantId;
                DELETE FROM opportunity_stage_history WHERE tenant_id = @TenantId;
                DELETE FROM opportunities WHERE tenant_id = @TenantId;
                DELETE FROM tenants WHERE id = @TenantId;
                """,
                admin);
            cleanup.Parameters.AddWithValue("TenantId", tenantId);
            await cleanup.ExecuteNonQueryAsync();
        }
    }
}
