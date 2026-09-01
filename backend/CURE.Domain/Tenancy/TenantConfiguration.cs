using System.Globalization;
using CURE.Domain.Intelligence;
using CURE.Domain.Shared;

namespace CURE.Domain.Tenancy;

/// Per-tenant policy values.

public sealed class TenantConfiguration
{
    public static class Keys
    {
        public const string HealthWeightEngagement = "health.weight.engagement";
        public const string HealthWeightRevenue = "health.weight.revenue";
        public const string HealthWeightSupport = "health.weight.support";
        public const string HealthWeightRelationship = "health.weight.relationship";
        public const string HealthWeightPayment = "health.weight.payment";
        public const string HealthWeightMomentum = "health.weight.momentum";

        public const string SignalSilenceDeviationFactor = "signal.silence.deviation_factor";
        public const string SignalSilenceMinimumDays = "signal.silence.minimum_days";
        public const string SignalStalledOpportunityDays = "signal.opportunity.stalled_days";
        public const string SignalRenewalHorizonDays = "signal.renewal.horizon_days";
        public const string SignalOpenCaseThreshold = "signal.support.open_case_threshold";
        public const string SignalConcentrationThreshold = "signal.relationship.concentration_threshold";

        public const string DuplicateReviewThreshold = "duplicate.review_threshold";
        public const string DuplicateBlockThreshold = "duplicate.block_threshold";

        public const string ExportMaxRows = "export.max_rows";
        public const string ExportApprovalRowThreshold = "export.approval_row_threshold";

        public const string SessionIdleMinutes = "session.idle_minutes";
        public const string SessionAbsoluteHours = "session.absolute_hours";
        public const string SensitiveActionReauthMinutes = "session.sensitive_reauth_minutes";

        public const string DiscountApprovalPercent = "sales.discount_approval_percent";
        public const string ExecutiveApprovalAmount = "sales.executive_approval_amount";

        public const string AiProcessingEnabled = "ai.processing_enabled";
        public const string BaselineMinimumInteractions = "intelligence.baseline_minimum_interactions";
    }

    private readonly Dictionary<string, string> _values;

    private TenantConfiguration(Dictionary<string, string> values) => _values = values;

    public static TenantConfiguration FromSettings(IEnumerable<KeyValuePair<string, string>> settings)
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var (key, value) in settings)
        {
            values[key] = value;
        }

        return new TenantConfiguration(values);
    }

    public static TenantConfiguration Defaults() => new(new Dictionary<string, string>(StringComparer.Ordinal));

    // ---- Customer health --------------------------------------------------

    /// <summary>
    /// Health dimension weights 
    public HealthWeights HealthWeights => HealthWeights.Create(
        engagement: Decimal(Keys.HealthWeightEngagement, 25m),
        revenue: Decimal(Keys.HealthWeightRevenue, 20m),
        support: Decimal(Keys.HealthWeightSupport, 20m),
        relationship: Decimal(Keys.HealthWeightRelationship, 15m),
        payment: Decimal(Keys.HealthWeightPayment, 10m),
        momentum: Decimal(Keys.HealthWeightMomentum, 10m));

    
    public decimal SilenceDeviationFactor => Decimal(Keys.SignalSilenceDeviationFactor, 2.5m);

    public int SilenceMinimumDays => Integer(Keys.SignalSilenceMinimumDays, 14);

    public int StalledOpportunityDays => Integer(Keys.SignalStalledOpportunityDays, 21);

    public int RenewalHorizonDays => Integer(Keys.SignalRenewalHorizonDays, 90);

    public int OpenCaseThreshold => Integer(Keys.SignalOpenCaseThreshold, 3);

    /// <summary>
    /// Share of commercial interaction concentrated on one contact before
    /// relationship-continuity risk is raised 
    public Percentage ConcentrationThreshold
        => Percentage.From(Decimal(Keys.SignalConcentrationThreshold, 70m));

    public int BaselineMinimumInteractions => Integer(Keys.BaselineMinimumInteractions, 5);


    /// <summary>Similarity at or above which the user is asked to review before creating.
    public Percentage DuplicateReviewThreshold
        => Percentage.From(Decimal(Keys.DuplicateReviewThreshold, 70m));

    /// <summary>
    /// Similarity at or above which creation is blocked pending an explicit
    /// decision. 
    public Percentage DuplicateBlockThreshold
        => Percentage.From(Decimal(Keys.DuplicateBlockThreshold, 92m));

    // ---- Export -----------------------------------------------------------

    public int ExportMaxRows => Integer(Keys.ExportMaxRows, 50_000);

    public int ExportApprovalRowThreshold => Integer(Keys.ExportApprovalRowThreshold, 5_000);

    // ---- Sessions ---------------------------------------------------------

    public TimeSpan SessionIdleTimeout => TimeSpan.FromMinutes(Integer(Keys.SessionIdleMinutes, 60));

    public TimeSpan SessionAbsoluteTimeout => TimeSpan.FromHours(Integer(Keys.SessionAbsoluteHours, 12));

    /// <summary>
    /// How recently the user must have authenticated to perform a sensitive
    
    public TimeSpan SensitiveActionReauthWindow
        => TimeSpan.FromMinutes(Integer(Keys.SensitiveActionReauthMinutes, 15));


    /// <summary>Discount percentage above which an approval is required </summary>
    public Percentage DiscountApprovalThreshold
        => Percentage.From(Decimal(Keys.DiscountApprovalPercent, 20m));

    /// <summary>Deal size above which executive approval is required.</summary>
    public decimal ExecutiveApprovalAmount => Decimal(Keys.ExecutiveApprovalAmount, 1_000_000m);

   
    /// Whether this tenant permits data to be sent to an external model
    
    public bool AiProcessingEnabled => Boolean(Keys.AiProcessingEnabled, false);

    // ---- Accessors --------------------------------------------------------

    public string? Raw(string key) => _values.TryGetValue(key, out var value) ? value : null;

    public IReadOnlyDictionary<string, string> All => _values;

    private decimal Decimal(string key, decimal fallback)
        => _values.TryGetValue(key, out var raw)
           && decimal.TryParse(raw, NumberStyles.Number, CultureInfo.InvariantCulture, out var value)
            ? value
            : fallback;

    private int Integer(string key, int fallback)
        => _values.TryGetValue(key, out var raw)
           && int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
            ? value
            : fallback;

    private bool Boolean(string key, bool fallback)
        => _values.TryGetValue(key, out var raw) && bool.TryParse(raw, out var value)
            ? value
            : fallback;
}
