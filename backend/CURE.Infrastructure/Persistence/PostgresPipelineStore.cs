using System.Data;
using System.Text.Json;
using CURE.Application.CoreCrm;
using CURE.Application.Pipeline;
using CURE.Domain.Pipeline;
using CURE.Domain.Shared;
using Dapper;

namespace CURE.Infrastructure.Persistence;

public sealed class PostgresPipelineStore(PostgresConnectionFactory connectionFactory) : IPipelineStore
{
    public async Task<PipelineSummaryDto> GetPipelineSummaryAsync(Guid tenantId, CancellationToken ct)
    {
        await using var connection = await connectionFactory.OpenAppAsync(tenantId, ct);

        const string sql = """
            SELECT
                stage AS Stage,
                COUNT(*)::int AS DealCount,
                COALESCE(SUM(amount), 0)::numeric AS TotalAmount,
                COALESCE(SUM(ROUND(amount * (probability::numeric / 100.0), 2)), 0)::numeric AS WeightedAmount,
                COALESCE(AVG(EXTRACT(EPOCH FROM (now() - stage_entered_at)) / 86400.0), 0)::float8 AS AverageDaysInStage
            FROM opportunities
            WHERE tenant_id = @TenantId
            GROUP BY stage;
            """;

        var rows = (await connection.QueryAsync<StageAggregationDto>(
            new CommandDefinition(sql, new { TenantId = tenantId }, cancellationToken: ct))).ToList();

        var stageMap = rows.ToDictionary(r => r.Stage, StringComparer.OrdinalIgnoreCase);

        var allStages = new[]
        {
            OpportunityStage.Qualification.ToString(),
            OpportunityStage.Discovery.ToString(),
            OpportunityStage.Proposal.ToString(),
            OpportunityStage.Negotiation.ToString(),
            OpportunityStage.ClosedWon.ToString(),
            OpportunityStage.ClosedLost.ToString(),
        };

        var stageSummaries = allStages.Select(stage =>
            stageMap.TryGetValue(stage, out var existing)
                ? existing
                : new StageAggregationDto(stage, 0, 0m, 0m, 0)).ToList();

        var openStages = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            OpportunityStage.Qualification.ToString(),
            OpportunityStage.Discovery.ToString(),
            OpportunityStage.Proposal.ToString(),
            OpportunityStage.Negotiation.ToString(),
        };

        var openDeals = stageSummaries.Where(s => openStages.Contains(s.Stage)).ToList();
        var totalPipelineAmount = openDeals.Sum(s => s.TotalAmount);
        var totalWeightedAmount = openDeals.Sum(s => s.WeightedAmount);
        var totalOpenDeals = openDeals.Sum(s => s.DealCount);

        var wonCount = stageMap.TryGetValue(OpportunityStage.ClosedWon.ToString(), out var won) ? won.DealCount : 0;
        var lostCount = stageMap.TryGetValue(OpportunityStage.ClosedLost.ToString(), out var lost) ? lost.DealCount : 0;
        var closedTotal = wonCount + lostCount;
        var winRate = closedTotal > 0 ? Math.Round((wonCount / (double)closedTotal) * 100.0, 1) : 0.0;

        return new PipelineSummaryDto(
            stageSummaries,
            totalPipelineAmount,
            totalWeightedAmount,
            totalOpenDeals,
            winRate);
    }

    public async Task<IReadOnlyList<OpportunityStageHistoryDto>> GetStageHistoryAsync(
        Guid tenantId,
        Guid opportunityId,
        CancellationToken ct)
    {
        await using var connection = await connectionFactory.OpenAppAsync(tenantId, ct);

        const string sql = """
            SELECT
                id AS Id,
                opportunity_id AS OpportunityId,
                from_stage AS FromStage,
                to_stage AS ToStage,
                amount AS Amount,
                currency AS Currency,
                probability AS Probability,
                weighted_amount AS WeightedAmount,
                days_in_prior_stage AS DaysInPriorStage,
                changed_at AS ChangedAt,
                changed_by AS ChangedBy,
                close_reason AS CloseReason,
                notes AS Notes
            FROM opportunity_stage_history
            WHERE tenant_id = @TenantId AND opportunity_id = @OpportunityId
            ORDER BY changed_at ASC;
            """;

        var rows = await connection.QueryAsync<OpportunityStageHistoryDto>(
            new CommandDefinition(sql, new { TenantId = tenantId, OpportunityId = opportunityId }, cancellationToken: ct));

        return rows.ToList();
    }

    public async Task<OpportunityDto> TransitionStageAsync(
        Guid tenantId,
        Guid actorId,
        string requestId,
        Guid opportunityId,
        TransitionStageCommand command,
        CancellationToken ct)
    {
        await using var connection = await connectionFactory.OpenAppAsync(tenantId, ct);
        await using var transaction = await connection.BeginTransactionAsync(ct);

        const string selectSql = """
            SELECT
                id AS Id,
                opportunity_number AS OpportunityNumber,
                customer_id AS CustomerId,
                name AS Name,
                stage AS Stage,
                amount AS Amount,
                currency AS Currency,
                probability AS Probability,
                expected_close_date AS ExpectedCloseDate,
                owner_id AS OwnerId,
                stage_entered_at AS StageEnteredAt,
                updated_at AS UpdatedAt,
                version AS Version
            FROM opportunities
            WHERE tenant_id = @TenantId AND id = @Id
            FOR UPDATE;
            """;

        var opp = await connection.QuerySingleOrDefaultAsync<OpportunityRecord>(
            new CommandDefinition(selectSql, new { TenantId = tenantId, Id = opportunityId }, transaction: transaction, cancellationToken: ct));

        if (opp is null)
        {
            throw DomainException.NotFound("Opportunity", opportunityId);
        }

        if (command.ExpectedVersion.HasValue && opp.Version != command.ExpectedVersion.Value)
        {
            throw new ConcurrencyException("Opportunity", command.ExpectedVersion.Value, opp.Version);
        }

        if (!Enum.TryParse<OpportunityStage>(command.TargetStage, true, out var targetStage))
        {
            throw new DomainException(ErrorCodes.OpportunityStageNotInPipeline, $"Unrecognized stage '{command.TargetStage}'.");
        }

        Enum.TryParse<OpportunityStage>(opp.Stage, true, out var currentStage);

        var amountMoney = Money.From(opp.Amount, opp.Currency);
        OpportunityStageGate.ValidateTransition(
            currentStage,
            targetStage,
            amountMoney,
            opp.CustomerId,
            command.CloseReason);

        var now = DateTimeOffset.UtcNow;
        var probability = targetStage.DefaultProbability().Value;
        var weighted = Math.Round(opp.Amount * (probability / 100.0m), 2);
        var daysInPrior = (int)Math.Max(0, (now - opp.StageEnteredAt).TotalDays);
        DateTimeOffset? closedAt = targetStage.IsClosed() ? now : null;

        const string updateSql = """
            UPDATE opportunities
            SET stage = @Stage,
                stage_entered_at = @Now,
                probability = @Probability,
                close_reason = @CloseReason,
                competitor_name = @CompetitorName,
                close_notes = @CloseNotes,
                closed_at = @ClosedAt,
                updated_at = @Now,
                version = version + 1
            WHERE tenant_id = @TenantId AND id = @Id AND version = @ExpectedVersion
            RETURNING
                id AS Id,
                opportunity_number AS OpportunityNumber,
                name AS Name,
                customer_id AS CustomerId,
                stage AS Stage,
                amount AS Amount,
                currency AS Currency,
                probability AS Probability,
                expected_close_date AS ExpectedCloseDate,
                owner_id AS OwnerId,
                updated_at AS UpdatedAt,
                version AS Version;
            """;

        var updated = await connection.QuerySingleOrDefaultAsync<OpportunityDto>(
            new CommandDefinition(
                updateSql,
                new
                {
                    TenantId = tenantId,
                    Id = opportunityId,
                    ExpectedVersion = opp.Version,
                    Stage = targetStage.ToString(),
                    Probability = (int)probability,
                    CloseReason = command.CloseReason,
                    CompetitorName = command.CompetitorName,
                    CloseNotes = command.Notes,
                    ClosedAt = closedAt,
                    Now = now,
                },
                transaction: transaction,
                cancellationToken: ct));

        if (updated is null)
        {
            throw new ConcurrencyException("Opportunity", opp.Version, null);
        }

        const string historySql = """
            INSERT INTO opportunity_stage_history (
                id, tenant_id, opportunity_id, from_stage, to_stage,
                amount, currency, probability, weighted_amount,
                days_in_prior_stage, changed_at, changed_by, close_reason, notes
            ) VALUES (
                @Id, @TenantId, @OpportunityId, @FromStage, @ToStage,
                @Amount, @Currency, @Probability, @WeightedAmount,
                @DaysInPriorStage, @Now, @ActorId, @CloseReason, @Notes
            );
            """;

        await connection.ExecuteAsync(new CommandDefinition(
            historySql,
            new
            {
                Id = Guid.CreateVersion7(),
                TenantId = tenantId,
                OpportunityId = opportunityId,
                FromStage = opp.Stage,
                ToStage = targetStage.ToString(),
                opp.Amount,
                opp.Currency,
                Probability = (int)probability,
                WeightedAmount = weighted,
                DaysInPriorStage = daysInPrior,
                Now = now,
                ActorId = actorId,
                CloseReason = command.CloseReason,
                command.Notes,
            },
            transaction: transaction,
            cancellationToken: ct));

        const string auditSql = """
            INSERT INTO audit_events (
                id, tenant_id, actor_id, action, entity_type, entity_id, request_id, occurred_at, before_state, after_state
            ) VALUES (
                @Id, @TenantId, @ActorId, 'OPPORTUNITY_STAGE_CHANGED', 'Opportunity', @EntityId, @RequestId, @Now, @Before::jsonb, @After::jsonb
            );
            """;

        await connection.ExecuteAsync(new CommandDefinition(
            auditSql,
            new
            {
                Id = Guid.CreateVersion7(),
                TenantId = tenantId,
                ActorId = actorId,
                EntityId = opportunityId,
                RequestId = requestId,
                Now = now,
                Before = JsonSerializer.Serialize(new { Stage = opp.Stage, opp.Probability, opp.Version }),
                After = JsonSerializer.Serialize(new { Stage = updated.Stage, updated.Probability, updated.Version, command.CloseReason }),
            },
            transaction: transaction,
            cancellationToken: ct));

        await transaction.CommitAsync(ct);
        return updated;
    }

    public async Task<ConvertedLeadResultDto> ConvertLeadAtomicAsync(
        Guid tenantId,
        Guid actorId,
        string requestId,
        Guid leadId,
        ConvertLeadCommand command,
        CancellationToken ct)
    {
        await using var connection = await connectionFactory.OpenAppAsync(tenantId, ct);
        await using var transaction = await connection.BeginTransactionAsync(ct);

        const string selectLeadSql = """
            SELECT
                id AS Id,
                lead_number AS LeadNumber,
                first_name AS FirstName,
                last_name AS LastName,
                company_name AS CompanyName,
                email AS Email,
                phone AS Phone,
                source AS Source,
                estimated_value AS EstimatedValue,
                status AS Status,
                owner_id AS OwnerId,
                version AS Version
            FROM leads
            WHERE tenant_id = @TenantId AND id = @Id
            FOR UPDATE;
            """;

        var lead = await connection.QuerySingleOrDefaultAsync<LeadRecord>(
            new CommandDefinition(selectLeadSql, new { TenantId = tenantId, Id = leadId }, transaction: transaction, cancellationToken: ct));

        if (lead is null)
        {
            throw DomainException.NotFound("Lead", leadId);
        }

        if (string.Equals(lead.Status, "CONVERTED", StringComparison.OrdinalIgnoreCase))
        {
            throw new DomainException(ErrorCodes.LeadAlreadyConverted, "This lead has already been converted.");
        }

        var now = DateTimeOffset.UtcNow;
        var customerId = Guid.CreateVersion7();
        var customerName = !string.IsNullOrWhiteSpace(command.CustomerName)
            ? command.CustomerName.Trim()
            : (!string.IsNullOrWhiteSpace(lead.CompanyName) ? lead.CompanyName.Trim() : $"{lead.FirstName} {lead.LastName}".Trim());

        var normalizedName = customerName.ToUpperInvariant();

        // 1. Create Customer
        const string insertCustomerSql = """
            INSERT INTO customers (
                id, tenant_id, name, normalized_name, customer_type, industry, email, normalized_email, phone, normalized_phone, owner_id, created_at, updated_at, version
            ) VALUES (
                @Id, @TenantId, @Name, @NormalizedName, @CustomerType, NULL, @Email, @NormalizedEmail, @Phone, @NormalizedPhone, @OwnerId, @Now, @Now, 1
            )
            ON CONFLICT (tenant_id, normalized_name) DO UPDATE SET updated_at = EXCLUDED.updated_at
            RETURNING id;
            """;

        var resolvedCustomerId = await connection.QuerySingleAsync<Guid>(new CommandDefinition(
            insertCustomerSql,
            new
            {
                Id = customerId,
                TenantId = tenantId,
                Name = customerName,
                NormalizedName = normalizedName,
                CustomerType = string.IsNullOrWhiteSpace(command.CustomerType) ? "Business" : command.CustomerType.Trim(),
                lead.Email,
                NormalizedEmail = lead.Email?.Trim().ToUpperInvariant(),
                lead.Phone,
                NormalizedPhone = lead.Phone?.Trim(),
                OwnerId = lead.OwnerId ?? actorId,
                Now = now,
            },
            transaction: transaction,
            cancellationToken: ct));

        // 2. Create Contact
        var contactId = Guid.CreateVersion7();
        const string insertContactSql = """
            INSERT INTO contacts (
                id, tenant_id, organization_id, first_name, last_name, email, phone, role, created_at, updated_at, version
            ) VALUES (
                @Id, @TenantId, NULL, @FirstName, @LastName, @Email, @Phone, 'Primary Lead Contact', @Now, @Now, 1
            );
            """;

        await connection.ExecuteAsync(new CommandDefinition(
            insertContactSql,
            new
            {
                Id = contactId,
                TenantId = tenantId,
                FirstName = string.IsNullOrWhiteSpace(lead.FirstName) ? lead.LastName : lead.FirstName,
                lead.LastName,
                lead.Email,
                lead.Phone,
                Now = now,
            },
            transaction: transaction,
            cancellationToken: ct));

        // Link Customer + Contact
        const string linkSql = """
            INSERT INTO customer_contact_links (tenant_id, customer_id, contact_id, linked_at)
            VALUES (@TenantId, @CustomerId, @ContactId, @Now)
            ON CONFLICT DO NOTHING;
            """;

        await connection.ExecuteAsync(new CommandDefinition(
            linkSql,
            new { TenantId = tenantId, CustomerId = resolvedCustomerId, ContactId = contactId, Now = now },
            transaction: transaction,
            cancellationToken: ct));

        // 3. Optional Opportunity
        Guid? opportunityId = null;
        if (command.CreateOpportunity || lead.EstimatedValue.HasValue)
        {
            opportunityId = Guid.CreateVersion7();
            var oppName = !string.IsNullOrWhiteSpace(command.OpportunityName)
                ? command.OpportunityName.Trim()
                : $"{customerName} - Initial Deal";
            var oppAmount = command.OpportunityAmount ?? lead.EstimatedValue ?? 10_000m;
            var oppCurrency = !string.IsNullOrWhiteSpace(command.Currency) ? command.Currency.Trim().ToUpperInvariant() : "USD";
            var oppNumber = "OPP-" + DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

            const string insertOppSql = """
                INSERT INTO opportunities (
                    id, tenant_id, opportunity_number, customer_id, name, stage, amount, currency, probability, expected_close_date, owner_id, stage_entered_at, created_at, updated_at, version
                ) VALUES (
                    @Id, @TenantId, @OppNumber, @CustomerId, @Name, 'Qualification', @Amount, @Currency, 10, @CloseDate, @OwnerId, @Now, @Now, @Now, 1
                );
                """;

            await connection.ExecuteAsync(new CommandDefinition(
                insertOppSql,
                new
                {
                    Id = opportunityId.Value,
                    TenantId = tenantId,
                    OppNumber = oppNumber,
                    CustomerId = resolvedCustomerId,
                    Name = oppName,
                    Amount = oppAmount,
                    Currency = oppCurrency,
                    CloseDate = command.ExpectedCloseDate ?? DateOnly.FromDateTime(DateTime.UtcNow.AddDays(30)),
                    OwnerId = lead.OwnerId ?? actorId,
                    Now = now,
                },
                transaction: transaction,
                cancellationToken: ct));

            // Initial stage history
            const string initialHistSql = """
                INSERT INTO opportunity_stage_history (
                    id, tenant_id, opportunity_id, from_stage, to_stage, amount, currency, probability, weighted_amount, days_in_prior_stage, changed_at, changed_by, close_reason, notes
                ) VALUES (
                    @Id, @TenantId, @OppId, NULL, 'Qualification', @Amount, @Currency, 10, @Weighted, 0, @Now, @ActorId, NULL, 'Created via Lead Conversion'
                );
                """;

            await connection.ExecuteAsync(new CommandDefinition(
                initialHistSql,
                new
                {
                    Id = Guid.CreateVersion7(),
                    TenantId = tenantId,
                    OppId = opportunityId.Value,
                    Amount = oppAmount,
                    Currency = oppCurrency,
                    Weighted = Math.Round(oppAmount * 0.10m, 2),
                    Now = now,
                    ActorId = actorId,
                },
                transaction: transaction,
                cancellationToken: ct));
        }

        // 4. Update Lead status
        const string updateLeadSql = """
            UPDATE leads
            SET status = 'CONVERTED',
                converted_customer_id = @CustomerId,
                converted_contact_id = @ContactId,
                converted_opportunity_id = @OpportunityId,
                converted_at = @Now,
                updated_at = @Now,
                version = version + 1
            WHERE tenant_id = @TenantId AND id = @Id;
            """;

        await connection.ExecuteAsync(new CommandDefinition(
            updateLeadSql,
            new
            {
                TenantId = tenantId,
                Id = leadId,
                CustomerId = resolvedCustomerId,
                ContactId = contactId,
                OpportunityId = opportunityId,
                Now = now,
            },
            transaction: transaction,
            cancellationToken: ct));

        // 5. Audit Event
        const string auditSql = """
            INSERT INTO audit_events (
                id, tenant_id, actor_id, action, entity_type, entity_id, request_id, occurred_at, before_state, after_state
            ) VALUES (
                @Id, @TenantId, @ActorId, 'LEAD_CONVERTED', 'Lead', @EntityId, @RequestId, @Now, @Before::jsonb, @After::jsonb
            );
            """;

        await connection.ExecuteAsync(new CommandDefinition(
            auditSql,
            new
            {
                Id = Guid.CreateVersion7(),
                TenantId = tenantId,
                ActorId = actorId,
                EntityId = leadId,
                RequestId = requestId,
                Now = now,
                Before = JsonSerializer.Serialize(new { lead.Status, lead.Version }),
                After = JsonSerializer.Serialize(new { Status = "CONVERTED", CustomerId = resolvedCustomerId, ContactId = contactId, OpportunityId = opportunityId }),
            },
            transaction: transaction,
            cancellationToken: ct));

        await transaction.CommitAsync(ct);

        return new ConvertedLeadResultDto(
            leadId,
            resolvedCustomerId,
            contactId,
            opportunityId,
            now);
    }

    private sealed record OpportunityRecord(
        Guid Id,
        string OpportunityNumber,
        Guid? CustomerId,
        string Name,
        string Stage,
        decimal Amount,
        string Currency,
        int Probability,
        DateOnly? ExpectedCloseDate,
        Guid? OwnerId,
        DateTimeOffset StageEnteredAt,
        DateTimeOffset UpdatedAt,
        int Version);

    private sealed record LeadRecord(
        Guid Id,
        string LeadNumber,
        string FirstName,
        string LastName,
        string? CompanyName,
        string? Email,
        string? Phone,
        string Source,
        decimal? EstimatedValue,
        string Status,
        Guid? OwnerId,
        int Version);
}
