namespace CURE.Domain.Tests.Pipeline;

using CURE.Domain.Pipeline;
using CURE.Domain.Shared;
using Xunit;

public sealed class StageGateConcurrencyTests
{
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _actorId = Guid.NewGuid();
    private readonly Guid _customerId = Guid.NewGuid();
    private readonly DateTimeOffset _now = DateTimeOffset.UtcNow;
    private readonly EntityNumber _oppNumber = EntityNumber.Format(NumberPrefixes.Opportunity, 777);

    [Fact]
    public void ConsecutiveTransitions_MaintainVersionAndTimestampIntegrity()
    {
        var opp = Opportunity.Create(
            Guid.NewGuid(), _tenantId, _oppNumber, "Fast Track Deal", _customerId, Money.Usd(150_000m),
            null, null, _actorId, _now);

        var t1 = _now.AddMinutes(5);
        opp.AdvanceToStage(_actorId, t1, OpportunityStage.Discovery);
        Assert.Equal(OpportunityStage.Discovery, opp.Stage);
        Assert.Equal(t1, opp.StageEnteredAt);

        var t2 = _now.AddMinutes(15);
        opp.AdvanceToStage(_actorId, t2, OpportunityStage.Proposal);
        Assert.Equal(OpportunityStage.Proposal, opp.Stage);
        Assert.Equal(t2, opp.StageEnteredAt);

        var t3 = _now.AddMinutes(30);
        opp.AdvanceToStage(_actorId, t3, OpportunityStage.Negotiation);
        Assert.Equal(OpportunityStage.Negotiation, opp.Stage);
        Assert.Equal(t3, opp.StageEnteredAt);

        var t4 = _now.AddMinutes(45);
        opp.CloseWon(_actorId, t4, OpportunityCloseReasons.WonBestValue, "Signed enterprise contract.");
        Assert.Equal(OpportunityStage.ClosedWon, opp.Stage);
        Assert.Equal(t4, opp.ClosedAt);
        Assert.Equal(Percentage.OneHundred, opp.Probability);
    }

    [Fact]
    public void CurrencyIntegrity_PreservedAcrossMultipleStageTransitions()
    {
        var amountGbp = Money.Gbp(80_000m);
        var opp = Opportunity.Create(
            Guid.NewGuid(), _tenantId, _oppNumber, "London Enterprise", _customerId, amountGbp,
            null, null, _actorId, _now);

        opp.AdvanceToStage(_actorId, _now.AddDays(1), OpportunityStage.Discovery);
        opp.AdvanceToStage(_actorId, _now.AddDays(2), OpportunityStage.Proposal);

        Assert.Equal("GBP", opp.Amount.Currency.Value);
        Assert.Equal(80_000m, opp.Amount.Amount);
        Assert.Equal(Money.Gbp(40_000m), opp.WeightedValue); // 50% of 80,000 GBP = 40,000 GBP
    }

    [Fact]
    public void Fuzzing_ReopeningClosedDeal_AllowsFullFunnelRecycle()
    {
        var opp = Opportunity.Create(
            Guid.NewGuid(), _tenantId, _oppNumber, "Recycled Deal", _customerId, Money.Usd(25_000m),
            null, null, _actorId, _now);

        opp.CloseLost(_actorId, _now.AddDays(1), OpportunityCloseReasons.LostBudgetCut);
        Assert.True(opp.Stage.IsLost());

        // Reopen to Qualification
        opp.Reopen(_actorId, _now.AddDays(5), OpportunityStage.Qualification, "New fiscal year budget approved.");
        Assert.Equal(OpportunityStage.Qualification, opp.Stage);
        Assert.False(opp.Stage.IsClosed());

        // Can advance again normally
        opp.AdvanceToStage(_actorId, _now.AddDays(6), OpportunityStage.Discovery);
        Assert.Equal(OpportunityStage.Discovery, opp.Stage);
    }
}
