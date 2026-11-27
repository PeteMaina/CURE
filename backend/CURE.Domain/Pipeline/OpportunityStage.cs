namespace CURE.Domain.Pipeline;

using CURE.Domain.Shared;

/// <summary>
/// Pipeline stages of an opportunity following standard B2B sales methodology.
/// </summary>
public enum OpportunityStage
{
    Qualification = 1,
    Discovery = 2,
    Proposal = 3,
    Negotiation = 4,
    ClosedWon = 5,
    ClosedLost = 6,
}

public static class OpportunityStageExtensions
{
    public static bool IsClosed(this OpportunityStage stage)
        => stage is OpportunityStage.ClosedWon or OpportunityStage.ClosedLost;

    public static bool IsOpen(this OpportunityStage stage)
        => !stage.IsClosed();

    public static bool IsWon(this OpportunityStage stage)
        => stage == OpportunityStage.ClosedWon;

    public static bool IsLost(this OpportunityStage stage)
        => stage == OpportunityStage.ClosedLost;

    public static Percentage DefaultProbability(this OpportunityStage stage) => stage switch
    {
        OpportunityStage.Qualification => Percentage.From(10m),
        OpportunityStage.Discovery => Percentage.From(25m),
        OpportunityStage.Proposal => Percentage.From(50m),
        OpportunityStage.Negotiation => Percentage.From(75m),
        OpportunityStage.ClosedWon => Percentage.OneHundred,
        OpportunityStage.ClosedLost => Percentage.Zero,
        _ => Percentage.Zero,
    };

    public static int SequenceOrder(this OpportunityStage stage) => (int)stage;

    public static string DisplayName(this OpportunityStage stage) => stage switch
    {
        OpportunityStage.Qualification => "Qualification",
        OpportunityStage.Discovery => "Discovery",
        OpportunityStage.Proposal => "Proposal",
        OpportunityStage.Negotiation => "Negotiation",
        OpportunityStage.ClosedWon => "Closed Won",
        OpportunityStage.ClosedLost => "Closed Lost",
        _ => stage.ToString(),
    };
}
