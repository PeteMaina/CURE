using CURE.Domain.Shared;

namespace CURE.Domain.Intelligence;

/// 
/// One reason the score is what it is.
///
/// This is the unit that answers "Why is this customer 64?" (). Every
/// factor carries the points it moved the score by and the observation behind
/// it. A factor with no evidence is not permitted.
/// 
public sealed record HealthFactor(
    string Code,
    HealthDimension Dimension,
    string Label,
    decimal Delta,
    string Evidence)
{
    public bool IsPositive => Delta > 0m;

    public bool IsNegative => Delta < 0m;
}

/// Coarse band for list views and filters, so colour is never the only signal ().
public enum HealthBand
{
    Critical = 0,
    AtRisk = 1,
    Neutral = 2,
    Healthy = 3,
    Strong = 4,
}

public static class HealthBands
{
    public static HealthBand For(decimal score) => score switch
    {
        < 25m => HealthBand.Critical,
        < 45m => HealthBand.AtRisk,
        < 65m => HealthBand.Neutral,
        < 85m => HealthBand.Healthy,
        _ => HealthBand.Strong,
    };

    public static string Label(this HealthBand band) => band switch
    {
        HealthBand.Critical => "Critical",
        HealthBand.AtRisk => "At risk",
        HealthBand.Neutral => "Neutral",
        HealthBand.Healthy => "Healthy",
        HealthBand.Strong => "Strong",
        _ => "Unknown",
    };
}

/// 
/// Everything the health calculator is allowed to look at.
///
/// Passing observations in as an immutable record keeps the calculator a pure
/// function: same inputs, same score, no database, no clock, no I/O. That is
/// what makes the property tests in  possible.
/// 
public sealed record HealthInputs
{
    // ---- Engagement -------------------------------------------------------
    public int? DaysSinceLastInteraction { get; init; }

    public int InteractionsLast30Days { get; init; }

    /// The customer's own normal gap between interactions ().
    public decimal? BaselineAverageGapDays { get; init; }

    public decimal? BaselineInteractionsPer30Days { get; init; }

    /// Interactions observed in a comparable earlier window, for trend.
    public int InteractionsPrevious30Days { get; init; }

    // ---- Revenue ----------------------------------------------------------
    public decimal RecognisedRevenueLast12Months { get; init; }

    public decimal RecognisedRevenuePrevious12Months { get; init; }

    public decimal OpenWeightedPipeline { get; init; }

    // ---- Support ----------------------------------------------------------
    public int OpenCases { get; init; }

    public int SlaBreachesLast90Days { get; init; }

    public int EscalationsLast90Days { get; init; }

    // ---- Relationship -----------------------------------------------------
    public int DistinctContactsEngagedLast90Days { get; init; }

    public bool DecisionMakerEngaged { get; init; }

    public bool ExecutiveRelationshipExists { get; init; }

    /// Share of recorded commercial interaction attributable to the single busiest contact.
    public Percentage? TopContactInteractionShare { get; init; }

    // ---- Payment ----------------------------------------------------------
    public int OverdueInvoiceCount { get; init; }

    public decimal AverageInvoiceDaysLate { get; init; }

    public bool HasInvoiceHistory { get; init; }

    // ---- Momentum ---------------------------------------------------------
    public int OpportunityStageAdvancesLast90Days { get; init; }

    public int StalledOpportunities { get; init; }

    public int CommitmentsDueLast90Days { get; init; }

    public int CommitmentsFulfilledOnTimeLast90Days { get; init; }

    // ---- Renewal exposure -------------------------------------------------
    public int? DaysToNextRenewal { get; init; }

    public bool RenewalAtRisk { get; init; }
}
