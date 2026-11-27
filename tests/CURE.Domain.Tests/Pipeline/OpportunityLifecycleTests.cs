namespace CURE.Domain.Tests.Pipeline;

using CURE.Domain.Pipeline;
using CURE.Domain.Pipeline.Events;
using CURE.Domain.Shared;
using Xunit;

public sealed class OpportunityLifecycleTests
{
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _actorId = Guid.NewGuid();
    private readonly Guid _customerId = Guid.NewGuid();
    private readonly DateTimeOffset _now = DateTimeOffset.UtcNow;
    private readonly EntityNumber _oppNumber = EntityNumber.Format(NumberPrefixes.Opportunity, 101);

    [Fact]
    public void Create_InitializesWithCorrectDefaultsAndEmitsEvent()
    {
        var amount = Money.Usd(50_000m);
        var opp = Opportunity.Create(
            Guid.NewGuid(),
            _tenantId,
            _oppNumber,
            "Acme Cloud Expansion",
            _customerId,
            amount,
            DateOnly.FromDateTime(DateTime.UtcNow.AddDays(30)),
            _actorId,
            _actorId,
            _now);

        Assert.Equal(OpportunityStage.Qualification, opp.Stage);
        Assert.Equal(Percentage.From(10m), opp.Probability);
        Assert.Equal(Money.Usd(5_000m), opp.WeightedValue);
        Assert.Single(opp.DomainEvents);
        Assert.IsType<OpportunityCreatedDomainEvent>(opp.DomainEvents[0]);
    }

    [Fact]
    public void AdvanceToStage_ValidTransitionUpdatesStageAndProbability()
    {
        var opp = Opportunity.Create(
            Guid.NewGuid(), _tenantId, _oppNumber, "Deal A", _customerId, Money.Usd(100_000m),
            null, null, _actorId, _now);

        opp.AdvanceToStage(_actorId, _now.AddDays(1), OpportunityStage.Discovery);

        Assert.Equal(OpportunityStage.Discovery, opp.Stage);
        Assert.Equal(Percentage.From(25m), opp.Probability);
        Assert.Equal(Money.Usd(25_000m), opp.WeightedValue);

        var stageEvent = Assert.IsType<OpportunityStageChangedDomainEvent>(opp.DomainEvents[1]);
        Assert.Equal(OpportunityStage.Qualification, stageEvent.OldStage);
        Assert.Equal(OpportunityStage.Discovery, stageEvent.NewStage);
    }

    [Fact]
    public void AdvanceToStage_RejectsIllegalSkipDirectlyToClosedWon()
    {
        var opp = Opportunity.Create(
            Guid.NewGuid(), _tenantId, _oppNumber, "Skip Deal", _customerId, Money.Usd(100_000m),
            null, null, _actorId, _now);

        var ex = Assert.Throws<DomainException>(() =>
            opp.AdvanceToStage(_actorId, _now.AddDays(1), OpportunityStage.ClosedWon));

        Assert.Equal(ErrorCodes.OpportunityInvalidStageTransition, ex.Code);
    }

    [Fact]
    public void CloseWon_EnforcesPositiveAmountCustomerIdAndReason()
    {
        var opp = Opportunity.Create(
            Guid.NewGuid(), _tenantId, _oppNumber, "Legit Deal", _customerId, Money.Usd(75_000m),
            null, null, _actorId, _now);

        // Advance through gates
        opp.AdvanceToStage(_actorId, _now.AddDays(1), OpportunityStage.Discovery);
        opp.AdvanceToStage(_actorId, _now.AddDays(2), OpportunityStage.Proposal);

        // Win deal
        opp.CloseWon(_actorId, _now.AddDays(3), OpportunityCloseReasons.WonBestValue, "Multi-year committed contract.");

        Assert.Equal(OpportunityStage.ClosedWon, opp.Stage);
        Assert.True(opp.Stage.IsWon());
        Assert.Equal(Percentage.OneHundred, opp.Probability);
        Assert.Equal(Money.Usd(75_000m), opp.WeightedValue);

        var wonEvent = Assert.IsType<OpportunityWonDomainEvent>(opp.DomainEvents.Last());
        Assert.Equal(OpportunityCloseReasons.WonBestValue, wonEvent.WinReason);
    }

    [Fact]
    public void CloseWon_FailsWhenAmountIsZeroOrNegative()
    {
        var opp = Opportunity.Create(
            Guid.NewGuid(), _tenantId, _oppNumber, "Zero Deal", _customerId, Money.Usd(0m),
            null, null, _actorId, _now);

        opp.AdvanceToStage(_actorId, _now.AddDays(1), OpportunityStage.Discovery);
        opp.AdvanceToStage(_actorId, _now.AddDays(2), OpportunityStage.Proposal);

        var ex = Assert.Throws<DomainException>(() =>
            opp.CloseWon(_actorId, _now.AddDays(3), OpportunityCloseReasons.WonBestValue));

        Assert.Equal(ErrorCodes.OpportunityAmountRequired, ex.Code);
    }

    [Fact]
    public void CloseLost_RequiresLossReason()
    {
        var opp = Opportunity.Create(
            Guid.NewGuid(), _tenantId, _oppNumber, "Lost Deal", _customerId, Money.Usd(20_000m),
            null, null, _actorId, _now);

        var ex = Assert.Throws<DomainException>(() =>
            opp.CloseLost(_actorId, _now.AddDays(1), "  "));

        Assert.Equal(ErrorCodes.OpportunityCloseReasonRequired, ex.Code);
    }

    [Fact]
    public void Reopen_RestoresClosedDealWithJustification()
    {
        var opp = Opportunity.Create(
            Guid.NewGuid(), _tenantId, _oppNumber, "Revived Deal", _customerId, Money.Usd(40_000m),
            null, null, _actorId, _now);

        opp.CloseLost(_actorId, _now.AddDays(1), OpportunityCloseReasons.LostTimingPostponed, competitor: null);

        opp.Reopen(_actorId, _now.AddDays(10), OpportunityStage.Discovery, "Customer reopened Q4 budget.");

        Assert.Equal(OpportunityStage.Discovery, opp.Stage);
        Assert.False(opp.Stage.IsClosed());
        Assert.Null(opp.ClosedAt);
        Assert.Equal(Percentage.From(25m), opp.Probability);

        var reopenedEvent = Assert.IsType<OpportunityReopenedDomainEvent>(opp.DomainEvents.Last());
        Assert.Equal(OpportunityStage.ClosedLost, reopenedEvent.PriorStage);
        Assert.Equal(OpportunityStage.Discovery, reopenedEvent.TargetStage);
    }
}
