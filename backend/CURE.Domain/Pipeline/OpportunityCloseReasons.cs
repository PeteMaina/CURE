namespace CURE.Domain.Pipeline;

/// <summary>
/// Standardized win and loss reasons for analytical win-rate and churn telemetry.
/// </summary>
public static class OpportunityCloseReasons
{
    // ---- Won Reasons ----
    public const string WonBestValue = "BEST_VALUE";
    public const string WonProductCapabilities = "PRODUCT_CAPABILITIES";
    public const string WonRelationship = "RELATIONSHIP_TRUST";
    public const string WonExecutiveSponsor = "EXECUTIVE_SPONSOR";
    public const string WonPricingDiscount = "PRICING_DISCOUNT";

    // ---- Lost Reasons ----
    public const string LostCompetitorSelected = "COMPETITOR_SELECTED";
    public const string LostBudgetCut = "BUDGET_CANCELLED";
    public const string LostAuthorityMissing = "DECISION_MAKER_UNAVAILABLE";
    public const string LostNeedDisappeared = "PROJECT_DEPRIORITIZED";
    public const string LostPriceTooHigh = "PRICE_TOO_HIGH";
    public const string LostTimingPostponed = "TIMING_POSTPONED";
    public const string LostGhosted = "GHOSTED_NO_RESPONSE";
}
