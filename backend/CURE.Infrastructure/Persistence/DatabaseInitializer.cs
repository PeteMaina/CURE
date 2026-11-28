using Dapper;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CURE.Infrastructure.Persistence;

public sealed class DatabaseInitializer(
    PostgresConnectionFactory connectionFactory,
    IOptions<DatabaseOptions> databaseOptions,
    IHostEnvironment environment,
    ILogger<DatabaseInitializer> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            if (environment.IsDevelopment())
            {
                await connectionFactory.EnsureDevelopmentRuntimeRoleAsync(cancellationToken);
            }

            await using var connection = await connectionFactory.OpenMigrationAsync(cancellationToken);
            await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
            await connection.ExecuteAsync(new Dapper.CommandDefinition(
                "SELECT pg_advisory_xact_lock(731942871);",
                transaction: transaction,
                cancellationToken: cancellationToken));
            await connection.ExecuteAsync(new Dapper.CommandDefinition(
                "CREATE TABLE IF NOT EXISTS cure_schema_migrations (version integer PRIMARY KEY, applied_at timestamptz NOT NULL DEFAULT now());",
                transaction: transaction,
                cancellationToken: cancellationToken));

            var applied = await connection.QuerySingleOrDefaultAsync<int?>(
                new Dapper.CommandDefinition(
                    "SELECT version FROM cure_schema_migrations WHERE version = 1;",
                    transaction: transaction,
                    cancellationToken: cancellationToken));
            if (applied is null)
            {
                await connection.ExecuteAsync(new Dapper.CommandDefinition(
                    Schema.Sql,
                    transaction: transaction,
                    cancellationToken: cancellationToken));
                await connection.ExecuteAsync(new Dapper.CommandDefinition(
                    "INSERT INTO cure_schema_migrations (version) VALUES (1);",
                    transaction: transaction,
                    cancellationToken: cancellationToken));
            }

            var hardeningApplied = await connection.QuerySingleOrDefaultAsync<int?>(
                new Dapper.CommandDefinition(
                    "SELECT version FROM cure_schema_migrations WHERE version = 2;",
                    transaction: transaction,
                    cancellationToken: cancellationToken));
            if (hardeningApplied is null)
            {
                await connection.ExecuteAsync(new Dapper.CommandDefinition(
                    Schema.TenantHardeningSql,
                    transaction: transaction,
                    cancellationToken: cancellationToken));
                await connection.ExecuteAsync(new Dapper.CommandDefinition(
                    "INSERT INTO cure_schema_migrations (version) VALUES (2);",
                    transaction: transaction,
                    cancellationToken: cancellationToken));
            }

            var pipelineApplied = await connection.QuerySingleOrDefaultAsync<int?>(
                new Dapper.CommandDefinition(
                    "SELECT version FROM cure_schema_migrations WHERE version = 3;",
                    transaction: transaction,
                    cancellationToken: cancellationToken));
            if (pipelineApplied is null)
            {
                await connection.ExecuteAsync(new Dapper.CommandDefinition(
                    Schema.PipelineEvolutionSql,
                    transaction: transaction,
                    cancellationToken: cancellationToken));
                await connection.ExecuteAsync(new Dapper.CommandDefinition(
                    "INSERT INTO cure_schema_migrations (version) VALUES (3);",
                    transaction: transaction,
                    cancellationToken: cancellationToken));
            }

            var engagementApplied = await connection.QuerySingleOrDefaultAsync<int?>(
                new Dapper.CommandDefinition(
                    "SELECT version FROM cure_schema_migrations WHERE version = 4;",
                    transaction: transaction,
                    cancellationToken: cancellationToken));
            if (engagementApplied is null)
            {
                await connection.ExecuteAsync(new Dapper.CommandDefinition(
                    Schema.EngagementEvolutionSql,
                    transaction: transaction,
                    cancellationToken: cancellationToken));
                await connection.ExecuteAsync(new Dapper.CommandDefinition(
                    "INSERT INTO cure_schema_migrations (version) VALUES (4);",
                    transaction: transaction,
                    cancellationToken: cancellationToken));
            }

            var grants = await connection.QuerySingleAsync<string>(new Dapper.CommandDefinition(
                """
                SELECT format(
                    'REVOKE ALL PRIVILEGES ON ALL TABLES IN SCHEMA public FROM %I;
                     REVOKE CREATE ON SCHEMA public FROM PUBLIC;
                     REVOKE ALL PRIVILEGES ON SCHEMA public FROM %I;
                     GRANT CONNECT ON DATABASE %I TO %I;
                     GRANT USAGE ON SCHEMA public TO %I;
                     GRANT SELECT ON cure_schema_migrations TO %I;
                     GRANT SELECT ON tenants TO %I;
                     GRANT SELECT ON users TO %I;
                     GRANT SELECT, INSERT, UPDATE, DELETE ON customers, organizations, contacts, leads, opportunities, activities, cases, saved_views, customer_contact_links, notification_states TO %I;
                     GRANT SELECT, INSERT ON audit_events TO %I;
                     REVOKE UPDATE, DELETE, TRUNCATE ON audit_events FROM %I',
                    @RoleName, @RoleName, current_database(), @RoleName, @RoleName, @RoleName, @RoleName, @RoleName, @RoleName, @RoleName, @RoleName);
                """,
                new { RoleName = databaseOptions.Value.RuntimeRoleName },
                transaction: transaction,
                cancellationToken: cancellationToken));
            await connection.ExecuteAsync(new Dapper.CommandDefinition(
                grants,
                transaction: transaction,
                cancellationToken: cancellationToken));
            await transaction.CommitAsync(cancellationToken);
            logger.LogInformation("CURE PostgreSQL schema is ready.");
        }
        catch (Npgsql.NpgsqlException exception)
        {
            logger.LogWarning(exception, "PostgreSQL is unavailable. Liveness remains available; readiness will report the dependency failure.");
        }

    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}

internal static class Schema
{
    public const string Sql = """
        CREATE EXTENSION IF NOT EXISTS pgcrypto;

        CREATE TABLE IF NOT EXISTS tenants (
            id uuid PRIMARY KEY,
            name text NOT NULL,
            slug text NOT NULL UNIQUE,
            status text NOT NULL DEFAULT 'ACTIVE',
            created_at timestamptz NOT NULL DEFAULT now()
        );

        CREATE TABLE IF NOT EXISTS users (
            id uuid PRIMARY KEY,
            tenant_id uuid NOT NULL REFERENCES tenants(id),
            display_name text NOT NULL,
            email text NOT NULL,
            role text NOT NULL DEFAULT 'Administrator',
            created_at timestamptz NOT NULL DEFAULT now(),
            UNIQUE (tenant_id, email)
        );

        CREATE TABLE IF NOT EXISTS customers (
            id uuid PRIMARY KEY,
            tenant_id uuid NOT NULL REFERENCES tenants(id),
            name text NOT NULL,
            normalized_name text NOT NULL,
            customer_type text NOT NULL,
            industry text NULL,
            email text NULL,
            normalized_email text NULL,
            phone text NULL,
            normalized_phone text NULL,
            owner_id uuid NULL REFERENCES users(id),
            created_at timestamptz NOT NULL DEFAULT now(),
            updated_at timestamptz NOT NULL DEFAULT now(),
            version integer NOT NULL DEFAULT 1,
            UNIQUE (tenant_id, normalized_name)
        );

        CREATE INDEX IF NOT EXISTS ix_customers_tenant_updated ON customers (tenant_id, updated_at DESC);
        CREATE INDEX IF NOT EXISTS ix_customers_tenant_email ON customers (tenant_id, normalized_email) WHERE normalized_email IS NOT NULL;
        CREATE INDEX IF NOT EXISTS ix_customers_tenant_phone ON customers (tenant_id, normalized_phone) WHERE normalized_phone IS NOT NULL;

        CREATE TABLE IF NOT EXISTS audit_events (
            id uuid PRIMARY KEY,
            tenant_id uuid NOT NULL REFERENCES tenants(id),
            actor_id uuid NOT NULL,
            action text NOT NULL,
            entity_type text NOT NULL,
            entity_id uuid NOT NULL,
            request_id text NOT NULL,
            occurred_at timestamptz NOT NULL DEFAULT now(),
            before_state jsonb NULL,
            after_state jsonb NULL
        );

        CREATE TABLE IF NOT EXISTS organizations (id uuid PRIMARY KEY, tenant_id uuid NOT NULL REFERENCES tenants(id), name text NOT NULL, normalized_name text NOT NULL, industry text, registration_number text, created_at timestamptz NOT NULL DEFAULT now(), updated_at timestamptz NOT NULL DEFAULT now(), version integer NOT NULL DEFAULT 1, UNIQUE (tenant_id, normalized_name));
        CREATE TABLE IF NOT EXISTS contacts (id uuid PRIMARY KEY, tenant_id uuid NOT NULL REFERENCES tenants(id), organization_id uuid NULL REFERENCES organizations(id), first_name text NOT NULL, last_name text NOT NULL, email text, phone text, role text, created_at timestamptz NOT NULL DEFAULT now(), updated_at timestamptz NOT NULL DEFAULT now(), version integer NOT NULL DEFAULT 1);
        CREATE TABLE IF NOT EXISTS leads (id uuid PRIMARY KEY, tenant_id uuid NOT NULL REFERENCES tenants(id), lead_number text NOT NULL, first_name text NOT NULL, last_name text NOT NULL, company_name text, email text, phone text, source text NOT NULL, estimated_value numeric(18,2), status text NOT NULL, owner_id uuid NULL REFERENCES users(id), created_at timestamptz NOT NULL DEFAULT now(), updated_at timestamptz NOT NULL DEFAULT now(), version integer NOT NULL DEFAULT 1, UNIQUE (tenant_id, lead_number));
        CREATE TABLE IF NOT EXISTS opportunities (id uuid PRIMARY KEY, tenant_id uuid NOT NULL REFERENCES tenants(id), opportunity_number text NOT NULL, customer_id uuid NULL REFERENCES customers(id), name text NOT NULL, stage text NOT NULL, amount numeric(18,2) NOT NULL CHECK (amount >= 0), currency char(3) NOT NULL, probability integer NOT NULL CHECK (probability BETWEEN 0 AND 100), expected_close_date date, owner_id uuid NULL REFERENCES users(id), created_at timestamptz NOT NULL DEFAULT now(), updated_at timestamptz NOT NULL DEFAULT now(), version integer NOT NULL DEFAULT 1, UNIQUE (tenant_id, opportunity_number));
        CREATE TABLE IF NOT EXISTS activities (id uuid PRIMARY KEY, tenant_id uuid NOT NULL REFERENCES tenants(id), type text NOT NULL, subject text NOT NULL, description text, customer_id uuid NULL REFERENCES customers(id), owner_id uuid NULL REFERENCES users(id), scheduled_at timestamptz, status text NOT NULL, created_at timestamptz NOT NULL DEFAULT now(), updated_at timestamptz NOT NULL DEFAULT now(), version integer NOT NULL DEFAULT 1);
        CREATE TABLE IF NOT EXISTS cases (id uuid PRIMARY KEY, tenant_id uuid NOT NULL REFERENCES tenants(id), case_number text NOT NULL, customer_id uuid NULL REFERENCES customers(id), subject text NOT NULL, description text, priority text NOT NULL, status text NOT NULL, owner_id uuid NULL REFERENCES users(id), created_at timestamptz NOT NULL DEFAULT now(), updated_at timestamptz NOT NULL DEFAULT now(), version integer NOT NULL DEFAULT 1, UNIQUE (tenant_id, case_number));
        CREATE TABLE IF NOT EXISTS saved_views (id uuid PRIMARY KEY, tenant_id uuid NOT NULL REFERENCES tenants(id), owner_id uuid NOT NULL REFERENCES users(id), name text NOT NULL, resource_type text NOT NULL, configuration_json jsonb NOT NULL, shared boolean NOT NULL DEFAULT false, created_at timestamptz NOT NULL DEFAULT now(), updated_at timestamptz NOT NULL DEFAULT now(), version integer NOT NULL DEFAULT 1);
        CREATE UNIQUE INDEX IF NOT EXISTS ux_users_tenant_id ON users (tenant_id, id);
        CREATE UNIQUE INDEX IF NOT EXISTS ux_customers_tenant_id ON customers (tenant_id, id);
        CREATE UNIQUE INDEX IF NOT EXISTS ux_contacts_tenant_id ON contacts (tenant_id, id);
        CREATE TABLE IF NOT EXISTS customer_contact_links (
            tenant_id uuid NOT NULL REFERENCES tenants(id),
            customer_id uuid NOT NULL,
            contact_id uuid NOT NULL,
            linked_at timestamptz NOT NULL DEFAULT now(),
            PRIMARY KEY (tenant_id, customer_id, contact_id),
            FOREIGN KEY (tenant_id, customer_id) REFERENCES customers(tenant_id, id) ON DELETE CASCADE,
            FOREIGN KEY (tenant_id, contact_id) REFERENCES contacts(tenant_id, id) ON DELETE CASCADE
        );
        CREATE TABLE IF NOT EXISTS notification_states (
            tenant_id uuid NOT NULL REFERENCES tenants(id),
            user_id uuid NOT NULL,
            entity_type text NOT NULL,
            entity_id uuid NOT NULL,
            read_at timestamptz NULL,
            updated_at timestamptz NOT NULL DEFAULT now(),
            PRIMARY KEY (tenant_id, user_id, entity_type, entity_id),
            FOREIGN KEY (tenant_id, user_id) REFERENCES users(tenant_id, id) ON DELETE CASCADE
        );
        CREATE INDEX IF NOT EXISTS ix_organizations_tenant_updated ON organizations (tenant_id, updated_at DESC);
        CREATE INDEX IF NOT EXISTS ix_contacts_tenant_updated ON contacts (tenant_id, updated_at DESC);
        CREATE INDEX IF NOT EXISTS ix_leads_tenant_updated ON leads (tenant_id, updated_at DESC);
        CREATE INDEX IF NOT EXISTS ix_opportunities_tenant_updated ON opportunities (tenant_id, updated_at DESC);
        CREATE INDEX IF NOT EXISTS ix_activities_tenant_updated ON activities (tenant_id, updated_at DESC);
        CREATE INDEX IF NOT EXISTS ix_cases_tenant_updated ON cases (tenant_id, updated_at DESC);
        CREATE INDEX IF NOT EXISTS ix_saved_views_tenant_updated ON saved_views (tenant_id, updated_at DESC);
        CREATE INDEX IF NOT EXISTS ix_customer_contact_links_tenant_customer ON customer_contact_links (tenant_id, customer_id);
        CREATE INDEX IF NOT EXISTS ix_customer_contact_links_tenant_contact ON customer_contact_links (tenant_id, contact_id);
        CREATE INDEX IF NOT EXISTS ix_notification_states_tenant_user_read ON notification_states (tenant_id, user_id, read_at);

        ALTER TABLE tenants ENABLE ROW LEVEL SECURITY;
        ALTER TABLE users ENABLE ROW LEVEL SECURITY;
        ALTER TABLE customers ENABLE ROW LEVEL SECURITY;
        ALTER TABLE audit_events ENABLE ROW LEVEL SECURITY;
        ALTER TABLE organizations ENABLE ROW LEVEL SECURITY;
        ALTER TABLE contacts ENABLE ROW LEVEL SECURITY;
        ALTER TABLE leads ENABLE ROW LEVEL SECURITY;
        ALTER TABLE opportunities ENABLE ROW LEVEL SECURITY;
        ALTER TABLE activities ENABLE ROW LEVEL SECURITY;
        ALTER TABLE cases ENABLE ROW LEVEL SECURITY;
        ALTER TABLE saved_views ENABLE ROW LEVEL SECURITY;
        ALTER TABLE customer_contact_links ENABLE ROW LEVEL SECURITY;
        ALTER TABLE notification_states ENABLE ROW LEVEL SECURITY;

        DROP POLICY IF EXISTS tenant_isolation_tenants ON tenants;
        CREATE POLICY tenant_isolation_tenants ON tenants USING (id::text = current_setting('app.tenant_id', true));
        DROP POLICY IF EXISTS tenant_isolation_users ON users;
        CREATE POLICY tenant_isolation_users ON users USING (tenant_id::text = current_setting('app.tenant_id', true));
        DROP POLICY IF EXISTS tenant_isolation_customers ON customers;
        CREATE POLICY tenant_isolation_customers ON customers USING (tenant_id::text = current_setting('app.tenant_id', true));
        DROP POLICY IF EXISTS tenant_isolation_audit ON audit_events;
        CREATE POLICY tenant_isolation_audit ON audit_events USING (tenant_id::text = current_setting('app.tenant_id', true));
        DROP POLICY IF EXISTS tenant_isolation_organizations ON organizations; CREATE POLICY tenant_isolation_organizations ON organizations USING (tenant_id::text = current_setting('app.tenant_id', true));
        DROP POLICY IF EXISTS tenant_isolation_contacts ON contacts; CREATE POLICY tenant_isolation_contacts ON contacts USING (tenant_id::text = current_setting('app.tenant_id', true));
        DROP POLICY IF EXISTS tenant_isolation_leads ON leads; CREATE POLICY tenant_isolation_leads ON leads USING (tenant_id::text = current_setting('app.tenant_id', true));
        DROP POLICY IF EXISTS tenant_isolation_opportunities ON opportunities; CREATE POLICY tenant_isolation_opportunities ON opportunities USING (tenant_id::text = current_setting('app.tenant_id', true));
        DROP POLICY IF EXISTS tenant_isolation_activities ON activities; CREATE POLICY tenant_isolation_activities ON activities USING (tenant_id::text = current_setting('app.tenant_id', true));
        DROP POLICY IF EXISTS tenant_isolation_cases ON cases; CREATE POLICY tenant_isolation_cases ON cases USING (tenant_id::text = current_setting('app.tenant_id', true));
        DROP POLICY IF EXISTS tenant_isolation_saved_views ON saved_views; CREATE POLICY tenant_isolation_saved_views ON saved_views USING (tenant_id::text = current_setting('app.tenant_id', true));
        DROP POLICY IF EXISTS tenant_isolation_customer_contact_links ON customer_contact_links;
        CREATE POLICY tenant_isolation_customer_contact_links ON customer_contact_links USING (tenant_id::text = current_setting('app.tenant_id', true)) WITH CHECK (tenant_id::text = current_setting('app.tenant_id', true));
        DROP POLICY IF EXISTS tenant_isolation_notification_states ON notification_states;
        CREATE POLICY tenant_isolation_notification_states ON notification_states USING (tenant_id::text = current_setting('app.tenant_id', true)) WITH CHECK (tenant_id::text = current_setting('app.tenant_id', true));

        INSERT INTO tenants (id, name, slug)
        VALUES ('00000000-0000-0000-0000-000000000001', 'CURE Industries', 'cure-industries')
        ON CONFLICT (id) DO NOTHING;
        INSERT INTO users (id, tenant_id, display_name, email, role)
        VALUES ('00000000-0000-0000-0000-000000000002', '00000000-0000-0000-0000-000000000001', 'Development Administrator', 'admin@cure.local', 'Administrator')
        ON CONFLICT (id) DO NOTHING;
        """;

    public const string TenantHardeningSql = """
        CREATE UNIQUE INDEX IF NOT EXISTS ux_organizations_tenant_id ON organizations (tenant_id, id);
        CREATE UNIQUE INDEX IF NOT EXISTS ux_leads_tenant_id ON leads (tenant_id, id);
        CREATE UNIQUE INDEX IF NOT EXISTS ux_opportunities_tenant_id ON opportunities (tenant_id, id);
        CREATE UNIQUE INDEX IF NOT EXISTS ux_activities_tenant_id ON activities (tenant_id, id);
        CREATE UNIQUE INDEX IF NOT EXISTS ux_cases_tenant_id ON cases (tenant_id, id);
        CREATE UNIQUE INDEX IF NOT EXISTS ux_saved_views_tenant_id ON saved_views (tenant_id, id);

        ALTER TABLE customers DROP CONSTRAINT IF EXISTS customers_owner_id_fkey;
        ALTER TABLE customers ADD CONSTRAINT fk_customers_owner_tenant
            FOREIGN KEY (tenant_id, owner_id) REFERENCES users (tenant_id, id);

        ALTER TABLE contacts DROP CONSTRAINT IF EXISTS contacts_organization_id_fkey;
        ALTER TABLE contacts ADD CONSTRAINT fk_contacts_organization_tenant
            FOREIGN KEY (tenant_id, organization_id) REFERENCES organizations (tenant_id, id);

        ALTER TABLE leads DROP CONSTRAINT IF EXISTS leads_owner_id_fkey;
        ALTER TABLE leads ADD CONSTRAINT fk_leads_owner_tenant
            FOREIGN KEY (tenant_id, owner_id) REFERENCES users (tenant_id, id);

        ALTER TABLE opportunities DROP CONSTRAINT IF EXISTS opportunities_customer_id_fkey;
        ALTER TABLE opportunities ADD CONSTRAINT fk_opportunities_customer_tenant
            FOREIGN KEY (tenant_id, customer_id) REFERENCES customers (tenant_id, id);
        ALTER TABLE opportunities DROP CONSTRAINT IF EXISTS opportunities_owner_id_fkey;
        ALTER TABLE opportunities ADD CONSTRAINT fk_opportunities_owner_tenant
            FOREIGN KEY (tenant_id, owner_id) REFERENCES users (tenant_id, id);

        ALTER TABLE activities DROP CONSTRAINT IF EXISTS activities_customer_id_fkey;
        ALTER TABLE activities ADD CONSTRAINT fk_activities_customer_tenant
            FOREIGN KEY (tenant_id, customer_id) REFERENCES customers (tenant_id, id);
        ALTER TABLE activities DROP CONSTRAINT IF EXISTS activities_owner_id_fkey;
        ALTER TABLE activities ADD CONSTRAINT fk_activities_owner_tenant
            FOREIGN KEY (tenant_id, owner_id) REFERENCES users (tenant_id, id);

        ALTER TABLE cases DROP CONSTRAINT IF EXISTS cases_customer_id_fkey;
        ALTER TABLE cases ADD CONSTRAINT fk_cases_customer_tenant
            FOREIGN KEY (tenant_id, customer_id) REFERENCES customers (tenant_id, id);
        ALTER TABLE cases DROP CONSTRAINT IF EXISTS cases_owner_id_fkey;
        ALTER TABLE cases ADD CONSTRAINT fk_cases_owner_tenant
            FOREIGN KEY (tenant_id, owner_id) REFERENCES users (tenant_id, id);

        ALTER TABLE saved_views DROP CONSTRAINT IF EXISTS saved_views_owner_id_fkey;
        ALTER TABLE saved_views ADD CONSTRAINT fk_saved_views_owner_tenant
            FOREIGN KEY (tenant_id, owner_id) REFERENCES users (tenant_id, id);

        ALTER TABLE tenants FORCE ROW LEVEL SECURITY;
        ALTER TABLE users FORCE ROW LEVEL SECURITY;
        ALTER TABLE customers FORCE ROW LEVEL SECURITY;
        ALTER TABLE audit_events FORCE ROW LEVEL SECURITY;
        ALTER TABLE organizations FORCE ROW LEVEL SECURITY;
        ALTER TABLE contacts FORCE ROW LEVEL SECURITY;
        ALTER TABLE leads FORCE ROW LEVEL SECURITY;
        ALTER TABLE opportunities FORCE ROW LEVEL SECURITY;
        ALTER TABLE activities FORCE ROW LEVEL SECURITY;
        ALTER TABLE cases FORCE ROW LEVEL SECURITY;
        ALTER TABLE saved_views FORCE ROW LEVEL SECURITY;
        ALTER TABLE customer_contact_links FORCE ROW LEVEL SECURITY;
        ALTER TABLE notification_states FORCE ROW LEVEL SECURITY;
        """;

    public const string EngagementEvolutionSql = """
        -- Enrich activities table with full engagement columns
        ALTER TABLE activities ADD COLUMN IF NOT EXISTS idempotency_key text NULL;
        ALTER TABLE activities ADD COLUMN IF NOT EXISTS activity_type text NULL;
        ALTER TABLE activities ADD COLUMN IF NOT EXISTS notes text NULL;
        ALTER TABLE activities ADD COLUMN IF NOT EXISTS completed_at timestamptz NULL;
        ALTER TABLE activities ADD COLUMN IF NOT EXISTS duration_minutes integer NULL;
        ALTER TABLE activities ADD COLUMN IF NOT EXISTS outcome text NULL;
        ALTER TABLE activities ADD COLUMN IF NOT EXISTS linked_customer_id uuid NULL;
        ALTER TABLE activities ADD COLUMN IF NOT EXISTS linked_contact_id uuid NULL;
        ALTER TABLE activities ADD COLUMN IF NOT EXISTS linked_opportunity_id uuid NULL;
        ALTER TABLE activities ADD COLUMN IF NOT EXISTS created_by uuid NULL;
        ALTER TABLE activities ADD COLUMN IF NOT EXISTS updated_by uuid NULL;

        -- Back-fill activity_type from legacy 'type' column so the new column is usable immediately
        UPDATE activities SET activity_type = type WHERE activity_type IS NULL;

        CREATE UNIQUE INDEX IF NOT EXISTS ux_activities_idempotency ON activities (tenant_id, idempotency_key) WHERE idempotency_key IS NOT NULL;
        CREATE INDEX IF NOT EXISTS ix_activities_customer_occurred ON activities (tenant_id, linked_customer_id, created_at DESC) WHERE linked_customer_id IS NOT NULL;
        CREATE INDEX IF NOT EXISTS ix_activities_opportunity ON activities (tenant_id, linked_opportunity_id, created_at DESC) WHERE linked_opportunity_id IS NOT NULL;

        -- audit_events: add domain-event columns used by the new activity store
        ALTER TABLE audit_events ADD COLUMN IF NOT EXISTS aggregate_type text NULL;
        ALTER TABLE audit_events ADD COLUMN IF NOT EXISTS aggregate_id uuid NULL;
        ALTER TABLE audit_events ADD COLUMN IF NOT EXISTS event_type text NULL;
        ALTER TABLE audit_events ADD COLUMN IF NOT EXISTS customer_id uuid NULL;
        ALTER TABLE audit_events ADD COLUMN IF NOT EXISTS occurred_at timestamptz NULL;
        ALTER TABLE audit_events ADD COLUMN IF NOT EXISTS payload jsonb NULL;

        CREATE INDEX IF NOT EXISTS ix_audit_events_customer_occurred ON audit_events (tenant_id, customer_id, occurred_at DESC) WHERE customer_id IS NOT NULL;

        -- Unified customer timeline view (keyset-paginated, newest-first)
        CREATE OR REPLACE VIEW customer_timeline_view AS
            SELECT
                a.id                       AS entry_id,
                a.tenant_id,
                a.linked_customer_id       AS customer_id,
                'Activity'                 AS entry_kind,
                COALESCE(a.activity_type, a.type) AS event_type,
                a.subject                  AS summary,
                a.notes                    AS detail,
                a.linked_opportunity_id,
                a.id                       AS linked_activity_id,
                a.owner_id                 AS actor_id,
                a.created_at               AS occurred_at
            FROM activities a
            WHERE a.linked_customer_id IS NOT NULL

            UNION ALL

            SELECT
                h.id                       AS entry_id,
                h.tenant_id,
                o.customer_id,
                'OpportunityStageChange'   AS entry_kind,
                'StageChanged'             AS event_type,
                ('Stage: ' || COALESCE(h.from_stage, 'New') || ' → ' || h.to_stage) AS summary,
                h.notes                    AS detail,
                h.opportunity_id           AS linked_opportunity_id,
                NULL::uuid                 AS linked_activity_id,
                h.changed_by               AS actor_id,
                h.changed_at               AS occurred_at
            FROM opportunity_stage_history h
            JOIN opportunities o ON o.id = h.opportunity_id AND o.tenant_id = h.tenant_id
            WHERE o.customer_id IS NOT NULL

            UNION ALL

            SELECT
                ae.id                      AS entry_id,
                ae.tenant_id,
                ae.customer_id,
                'AuditEvent'               AS entry_kind,
                COALESCE(ae.event_type, ae.action) AS event_type,
                COALESCE(ae.entity_type, ae.aggregate_type, 'System') AS summary,
                NULL::text                 AS detail,
                ae.aggregate_id            AS linked_opportunity_id,
                NULL::uuid                 AS linked_activity_id,
                ae.actor_id,
                COALESCE(ae.occurred_at, ae.occurred_at, now()) AS occurred_at
            FROM audit_events ae
            WHERE ae.customer_id IS NOT NULL;
        """;

    public const string PipelineEvolutionSql = """
        CREATE TABLE IF NOT EXISTS opportunity_stage_history (
            id uuid PRIMARY KEY,
            tenant_id uuid NOT NULL REFERENCES tenants(id),
            opportunity_id uuid NOT NULL REFERENCES opportunities(id) ON DELETE CASCADE,
            from_stage text NULL,
            to_stage text NOT NULL,
            amount numeric(18,2) NOT NULL,
            currency char(3) NOT NULL,
            probability integer NOT NULL,
            weighted_amount numeric(18,2) NOT NULL,
            days_in_prior_stage integer NULL,
            changed_at timestamptz NOT NULL DEFAULT now(),
            changed_by uuid NOT NULL,
            close_reason text NULL,
            notes text NULL
        );

        CREATE INDEX IF NOT EXISTS ix_stage_hist_tenant_opp ON opportunity_stage_history (tenant_id, opportunity_id, changed_at DESC);
        CREATE INDEX IF NOT EXISTS ix_stage_hist_tenant_stage ON opportunity_stage_history (tenant_id, to_stage, changed_at DESC);

        ALTER TABLE opportunities ADD COLUMN IF NOT EXISTS stage_entered_at timestamptz NOT NULL DEFAULT now();
        ALTER TABLE opportunities ADD COLUMN IF NOT EXISTS close_reason text NULL;
        ALTER TABLE opportunities ADD COLUMN IF NOT EXISTS competitor_name text NULL;
        ALTER TABLE opportunities ADD COLUMN IF NOT EXISTS close_notes text NULL;
        ALTER TABLE opportunities ADD COLUMN IF NOT EXISTS closed_at timestamptz NULL;

        ALTER TABLE leads ADD COLUMN IF NOT EXISTS converted_customer_id uuid NULL REFERENCES customers(id);
        ALTER TABLE leads ADD COLUMN IF NOT EXISTS converted_contact_id uuid NULL REFERENCES contacts(id);
        ALTER TABLE leads ADD COLUMN IF NOT EXISTS converted_opportunity_id uuid NULL REFERENCES opportunities(id);
        ALTER TABLE leads ADD COLUMN IF NOT EXISTS converted_at timestamptz NULL;
        ALTER TABLE leads ADD COLUMN IF NOT EXISTS disqualification_reason text NULL;

        ALTER TABLE opportunity_stage_history ENABLE ROW LEVEL SECURITY;
        ALTER TABLE opportunity_stage_history FORCE ROW LEVEL SECURITY;
        DROP POLICY IF EXISTS tenant_isolation_stage_hist ON opportunity_stage_history;
        CREATE POLICY tenant_isolation_stage_hist ON opportunity_stage_history USING (tenant_id::text = current_setting('app.tenant_id', true));
        """;
}