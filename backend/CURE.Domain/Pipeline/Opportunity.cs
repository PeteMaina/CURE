namespace CURE.Domain.Pipeline;

using CURE.Domain.Pipeline.Events;
using CURE.Domain.Shared;

/// <summary>
/// Opportunity aggregate root representing a qualified commercial deal pipeline.
/// </summary>
public sealed class Opportunity : AggregateRoot
{
    private Opportunity(
        Guid id,
        Guid tenantId,
        EntityNumber opportunityNumber,
        string name,
        Guid? customerId,
        OpportunityStage stage,
        Money amount,
        Percentage probability,
        DateOnly? expectedCloseDate,
        Guid? ownerId,
        DateTimeOffset stageEnteredAt,
        string? closeReason,
        string? competitorName,
        string? closeNotes,
        DateTimeOffset? closedAt,
        DateTimeOffset createdAt,
        Guid createdBy,
        DateTimeOffset updatedAt,
        Guid updatedBy,
        int version)
        : base(id, tenantId, createdAt, createdBy, updatedAt, updatedBy, version)
    {
        OpportunityNumber = opportunityNumber;
        Name = name;
        CustomerId = customerId;
        Stage = stage;
        Amount = amount;
        Probability = probability;
        ExpectedCloseDate = expectedCloseDate;
        OwnerId = ownerId;
        StageEnteredAt = stageEnteredAt;
        CloseReason = closeReason;
        CompetitorName = competitorName;
        CloseNotes = closeNotes;
        ClosedAt = closedAt;
    }

    public EntityNumber OpportunityNumber { get; }

    public string Name { get; private set; }

    public Guid? CustomerId { get; private set; }

    public OpportunityStage Stage { get; private set; }

    public Money Amount { get; private set; }

    public Percentage Probability { get; private set; }

    public DateOnly? ExpectedCloseDate { get; private set; }

    public Guid? OwnerId { get; private set; }

    public DateTimeOffset StageEnteredAt { get; private set; }

    public string? CloseReason { get; private set; }

    public string? CompetitorName { get; private set; }

    public string? CloseNotes { get; private set; }

    public DateTimeOffset? ClosedAt { get; private set; }

    /// <summary>
    /// Calculated weighted pipeline value for revenue forecasting.
    /// </summary>
    public Money WeightedValue => Amount.MultiplyBy(Probability.AsFraction).Round();

    public static Opportunity Create(
        Guid id,
        Guid tenantId,
        EntityNumber opportunityNumber,
        string name,
        Guid? customerId,
        Money amount,
        DateOnly? expectedCloseDate,
        Guid? ownerId,
        Guid actorId,
        DateTimeOffset at,
        OpportunityStage initialStage = OpportunityStage.Qualification,
        Percentage? initialProbability = null)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw DomainException.Validation("An opportunity name is required.");
        }

        if (initialStage.IsClosed())
        {
            throw new DomainException(
                ErrorCodes.OpportunityInvalidStageTransition,
                "A new opportunity cannot be initialized directly in a closed stage.");
        }

        var probability = initialProbability ?? initialStage.DefaultProbability();

        var opp = new Opportunity(
            id: id,
            tenantId: tenantId,
            opportunityNumber: opportunityNumber,
            name: name.Trim(),
            customerId: customerId,
            stage: initialStage,
            amount: amount,
            probability: probability,
            expectedCloseDate: expectedCloseDate,
            ownerId: ownerId,
            stageEnteredAt: at,
            closeReason: null,
            competitorName: null,
            closeNotes: null,
            closedAt: null,
            createdAt: at,
            createdBy: actorId,
            updatedAt: at,
            updatedBy: actorId,
            version: 1);

        opp.Raise(new OpportunityCreatedDomainEvent(
            tenantId,
            at,
            id,
            opportunityNumber.Value,
            name.Trim(),
            customerId,
            initialStage,
            amount,
            probability,
            actorId));

        return opp;
    }

    public void AdvanceToStage(
        Guid actorId,
        DateTimeOffset at,
        OpportunityStage targetStage,
        Percentage? customProbability = null,
        bool isOverride = false)
    {
        OpportunityStageGate.ValidateTransition(
            Stage,
            targetStage,
            Amount,
            CustomerId,
            closeReason: null,
            isOverride: isOverride);

        var oldStage = Stage;
        Stage = targetStage;
        StageEnteredAt = at;
        Probability = customProbability ?? targetStage.DefaultProbability();
        Touch(actorId, at);

        Raise(new OpportunityStageChangedDomainEvent(
            TenantId,
            at,
            Id,
            OpportunityNumber.Value,
            CustomerId,
            oldStage,
            targetStage,
            Amount,
            Probability,
            actorId));
    }

    public void CloseWon(Guid actorId, DateTimeOffset at, string winReason, string? notes = null)
    {
        OpportunityStageGate.ValidateTransition(
            Stage,
            OpportunityStage.ClosedWon,
            Amount,
            CustomerId,
            winReason);

        var oldStage = Stage;
        Stage = OpportunityStage.ClosedWon;
        StageEnteredAt = at;
        CloseReason = winReason.Trim();
        CloseNotes = notes?.Trim();
        ClosedAt = at;
        Probability = Percentage.OneHundred;
        Touch(actorId, at);

        Raise(new OpportunityWonDomainEvent(
            TenantId,
            at,
            Id,
            OpportunityNumber.Value,
            CustomerId!.Value,
            Amount,
            winReason.Trim(),
            actorId));
    }

    public void CloseLost(Guid actorId, DateTimeOffset at, string lossReason, string? competitor = null, string? notes = null)
    {
        OpportunityStageGate.ValidateTransition(
            Stage,
            OpportunityStage.ClosedLost,
            Amount,
            CustomerId,
            lossReason);

        var oldStage = Stage;
        Stage = OpportunityStage.ClosedLost;
        StageEnteredAt = at;
        CloseReason = lossReason.Trim();
        CompetitorName = competitor?.Trim();
        CloseNotes = notes?.Trim();
        ClosedAt = at;
        Probability = Percentage.Zero;
        Touch(actorId, at);

        Raise(new OpportunityLostDomainEvent(
            TenantId,
            at,
            Id,
            OpportunityNumber.Value,
            CustomerId,
            Amount,
            lossReason.Trim(),
            competitor?.Trim(),
            actorId));
    }

    public void Reopen(Guid actorId, DateTimeOffset at, OpportunityStage targetStage, string justification)
    {
        if (string.IsNullOrWhiteSpace(justification))
        {
            throw DomainException.Validation("A justification is required to reopen a closed opportunity.");
        }

        if (!Stage.IsClosed())
        {
            throw DomainException.Validation("Only closed opportunities can be reopened.");
        }

        if (targetStage.IsClosed())
        {
            throw DomainException.Validation("The reopen target stage must be an active open stage.");
        }

        var priorStage = Stage;
        Stage = targetStage;
        StageEnteredAt = at;
        CloseReason = null;
        CompetitorName = null;
        CloseNotes = null;
        ClosedAt = null;
        Probability = targetStage.DefaultProbability();
        Touch(actorId, at);

        Raise(new OpportunityReopenedDomainEvent(
            TenantId,
            at,
            Id,
            OpportunityNumber.Value,
            priorStage,
            targetStage,
            justification.Trim(),
            actorId));
    }

    public void UpdateAmount(Guid actorId, DateTimeOffset at, Money newAmount)
    {
        if (Stage.IsWon() && newAmount.Amount <= 0)
        {
            throw new DomainException(
                ErrorCodes.OpportunityAmountRequired,
                "A Closed-Won deal cannot have its value reduced to zero.");
        }

        Amount = newAmount;
        Touch(actorId, at);
    }

    public void AssignCustomer(Guid actorId, DateTimeOffset at, Guid customerId)
    {
        if (customerId == Guid.Empty)
        {
            throw DomainException.Validation("A valid customer identifier must be provided.");
        }

        CustomerId = customerId;
        Touch(actorId, at);
    }

    public void AssignOwner(Guid actorId, DateTimeOffset at, Guid? ownerId)
    {
        OwnerId = ownerId;
        Touch(actorId, at);
    }

    public void SetExpectedCloseDate(Guid actorId, DateTimeOffset at, DateOnly? expectedCloseDate)
    {
        ExpectedCloseDate = expectedCloseDate;
        Touch(actorId, at);
    }

    public void SetProbability(Guid actorId, DateTimeOffset at, Percentage probability)
    {
        if (Stage.IsClosed())
        {
            throw new DomainException(
                ErrorCodes.OpportunityAlreadyClosed,
                "Cannot manually alter probability on a closed deal.");
        }

        Probability = probability;
        Touch(actorId, at);
    }
}
