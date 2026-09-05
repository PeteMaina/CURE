using CURE.Domain.Shared;

namespace CURE.Domain.Intelligence;

/// Risk thresholds the calculator consults. Supplied from tenant configuration.</summary>
public sealed record HealthRiskPolicy(Percentage ConcentrationThreshold, int RenewalHorizonDays)
{
    public static HealthRiskPolicy Default { get; } =
        new(Percentage.From(70m), 90);
}

/// One dimension's contribution, shown on the customer Health tab.</summary>
public sealed record HealthDimensionScore(
    HealthDimension Dimension,
    decimal Score,
    string Label,
    string Evidence,
    decimal WeightShare,
    decimal Contribution);

/// The complete, reconcilable result of a health calculation.</summary>
public sealed record HealthAssessment(
    decimal Score,
    HealthBand Band,
    IReadOnlyList<HealthDimensionScore> Dimensions,
    IReadOnlyList<HealthFactor> Factors,
    string WeightsFingerprint,
    int CalculationVersion)
{
    /// 
    /// The neutral starting point plus every factor equals the score. The UI
    /// relies on this to render an explanation that adds up.
    /// </summary>
    public bool Reconciles =>
        Math.Abs(
            CustomerHealthCalculator.NeutralBaseline
            + Factors.Sum(factor => factor.Delta)
            - Score) < 0.01m;
}

/// 
/// Computes relationship health from observable facts.
///
/// Design rules this class exists to enforce:
///
///   * <b>Deterministic.</b> A pure function of (inputs, weights, policy). No
///     clock, no database, no randomness — so the same customer state always
///     produces the same score and the property tests can assert bounds.
///   * <b>Explainable.</b> Every point of movement is attributed to a named
///     factor with evidence. "AI says 64" is explicitly forbidden ().
///   * <b>Bounded.</b> The result is always 0–100. Dimension contributions are
///     algebraically incapable of leaving that range; explicit risk adjustments
///     can, and are clamped with a visible factor when they do.
///   * <b>Not machine learning.</b> These are deterministic rules and are
///     labelled as such (). Replacing them later with a model is a
///     matter of implementing ICustomerRiskProvider, not rewriting callers.
///
/// The scale is anchored at <see cref="NeutralBaseline"/> rather than zero so
/// that "we know nothing about this customer" scores 50, not 0. A brand-new
/// customer is not unhealthy; it is unmeasured.
/// </summary>
public static class CustomerHealthCalculator
{
    /// 
    /// Bump when the formula changes. Stored on every snapshot so a score from
    /// six months ago can still be interpreted (, 150).
    /// </summary>
    public const int CalculationVersion = 1;

    public const decimal NeutralBaseline = 50m;

    /// Assumed interaction gap when a customer has no baseline yet.</summary>
    private const decimal AssumedGapDaysWithoutBaseline = 14m;

    /// Assumed monthly interaction count when a customer has no baseline yet.</summary>
    private const decimal AssumedInteractionsPer30DaysWithoutBaseline = 2m;

    /// Commitment-completion rate treated as "meeting expectations".</summary>
    private const decimal ExpectedCommitmentCompletionRate = 0.8m;

    public static HealthAssessment Calculate(
        HealthInputs inputs,
        HealthWeights weights,
        HealthRiskPolicy policy)
    {
        ArgumentNullException.ThrowIfNull(inputs);
        ArgumentNullException.ThrowIfNull(weights);
        ArgumentNullException.ThrowIfNull(policy);

        var dimensions = new List<HealthDimensionScore>(6);
        var factors = new List<HealthFactor>();

        AddDimension(dimensions, factors, weights, HealthDimension.Engagement, ScoreEngagement(inputs));
        AddDimension(dimensions, factors, weights, HealthDimension.Revenue, ScoreRevenue(inputs));
        AddDimension(dimensions, factors, weights, HealthDimension.Support, ScoreSupport(inputs));
        AddDimension(dimensions, factors, weights, HealthDimension.Relationship, ScoreRelationship(inputs));
        AddDimension(dimensions, factors, weights, HealthDimension.Payment, ScorePayment(inputs));
        AddDimension(dimensions, factors, weights, HealthDimension.Momentum, ScoreMomentum(inputs));

        factors.AddRange(RiskAdjustments(inputs, policy));

        var raw = NeutralBaseline + factors.Sum(factor => factor.Delta);
        var score = Math.Round(Math.Clamp(raw, 0m, 100m), 1, MidpointRounding.ToEven);

        // If clamping moved the number, say so rather than silently disagreeing
        // with the arithmetic shown to the user.
        if (Math.Abs(raw - score) >= 0.05m)
        {
            factors.Add(new HealthFactor(
                Code: "SCALE_BOUND_APPLIED",
                Dimension: HealthDimension.Risk,
                Label: raw > 100m ? "Capped at maximum" : "Floored at minimum",
                Delta: score - raw,
                Evidence: $"Raw calculation was {raw:0.#}; the score is reported on a 0–100 scale."));
        }

        return new HealthAssessment(
            Score: score,
            Band: HealthBands.For(score),
            Dimensions: dimensions,
            Factors: factors,
            WeightsFingerprint: weights.Fingerprint,
            CalculationVersion: CalculationVersion);
    }

    private static void AddDimension(
        List<HealthDimensionScore> dimensions,
        List<HealthFactor> factors,
        HealthWeights weights,
        HealthDimension dimension,
        DimensionResult result)
    {
        var share = weights.ShareOf(dimension);
        var contribution = Math.Round((result.Score - NeutralBaseline) * share, 2, MidpointRounding.ToEven);

        dimensions.Add(new HealthDimensionScore(
            dimension,
            Math.Round(result.Score, 1, MidpointRounding.ToEven),
            result.Label,
            result.Evidence,
            Math.Round(share * 100m, 1, MidpointRounding.ToEven),
            contribution));

        // A zero-weight dimension is still scored and shown, but contributes
        // nothing and so is not listed as a reason for the overall number.
        if (share > 0m)
        {
            factors.Add(new HealthFactor(
                Code: $"DIMENSION_{dimension.ToString().ToUpperInvariant()}",
                Dimension: dimension,
                Label: result.Label,
                Delta: contribution,
                Evidence: result.Evidence));
        }
    }

    private sealed record DimensionResult(decimal Score, string Label, string Evidence);

    // ---- Engagement -------------------------------------------------------

    /// 
    /// Recency and frequency, each measured against this customer's own history
    /// rather than a global rule (, 183).
    /// </summary>
    private static DimensionResult ScoreEngagement(HealthInputs inputs)
    {
        if (inputs.DaysSinceLastInteraction is null)
        {
            return new DimensionResult(
                25m,
                "No interaction recorded",
                "No interaction has ever been recorded against this customer.");
        }

        var days = (decimal)inputs.DaysSinceLastInteraction.Value;
        var baselineGap = Math.Max(inputs.BaselineAverageGapDays ?? AssumedGapDaysWithoutBaseline, 1m);
        var gapRatio = days / baselineGap;

        // At or better than normal: full marks. Four times normal: zero.
        var recency = gapRatio <= 1m
            ? 100m
            : gapRatio >= 4m
                ? 0m
                : 100m * (4m - gapRatio) / 3m;

        var baselineFrequency = Math.Max(
            inputs.BaselineInteractionsPer30Days ?? AssumedInteractionsPer30DaysWithoutBaseline,
            0.5m);

        // Matching the customer's normal cadence scores 70; exceeding it earns the rest.
        var frequency = Math.Clamp(inputs.InteractionsLast30Days / baselineFrequency * 70m, 0m, 100m);

        var score = (recency * 0.6m) + (frequency * 0.4m);

        var hasBaseline = inputs.BaselineAverageGapDays is not null;
        var deviation = days - baselineGap;

        var label = score >= 70m ? "Engagement steady"
            : score >= 45m ? "Engagement softening"
            : "Engagement declined";

        var evidence = hasBaseline
            ? $"Last interaction {days:0} day(s) ago against a normal gap of {baselineGap:0.#} day(s) " +
              $"({(deviation >= 0 ? "+" : string.Empty)}{deviation:0.#}). " +
              $"{inputs.InteractionsLast30Days} interaction(s) in the last 30 days versus a typical {baselineFrequency:0.#}."
            : $"Last interaction {days:0} day(s) ago. Not enough history yet for a customer-specific baseline, " +
              $"so a {AssumedGapDaysWithoutBaseline:0} day cadence is assumed.";

        return new DimensionResult(score, label, evidence);
    }

    // ---- Revenue ----------------------------------------------------------

    private static DimensionResult ScoreRevenue(HealthInputs inputs)
    {
        var current = inputs.RecognisedRevenueLast12Months;
        var prior = inputs.RecognisedRevenuePrevious12Months;

        if (current <= 0m && prior <= 0m)
        {
            var pipelineNote = inputs.OpenWeightedPipeline > 0m
                ? $" Weighted open pipeline is {inputs.OpenWeightedPipeline:N0}."
                : string.Empty;

            return new DimensionResult(
                NeutralBaseline,
                "No revenue history",
                $"No recognised revenue in either of the last two 12-month periods.{pipelineNote}");
        }

        if (prior <= 0m)
        {
            return new DimensionResult(
                75m,
                "Revenue established",
                $"First recognised revenue of {current:N0} in the last 12 months, with no prior-period comparison.");
        }

        var growth = (current - prior) / prior;

        // +50% year on year reaches the top of the scale; -50% reaches the bottom.
        var score = Math.Clamp(NeutralBaseline + (growth * 100m), 0m, 100m);

        var label = growth >= 0.10m ? "Revenue growing"
            : growth >= -0.05m ? "Revenue stable"
            : "Revenue contracting";

        return new DimensionResult(
            score,
            label,
            $"Recognised revenue {current:N0} versus {prior:N0} in the prior 12 months " +
            $"({(growth >= 0 ? "+" : string.Empty)}{growth * 100m:0.#}%).");
    }

    // ---- Support ----------------------------------------------------------

    private static DimensionResult ScoreSupport(HealthInputs inputs)
    {
        const decimal PenaltyPerOpenCase = 12m;
        const decimal PenaltyPerSlaBreach = 20m;
        const decimal PenaltyPerEscalation = 10m;

        var score = 100m
            - (PenaltyPerOpenCase * inputs.OpenCases)
            - (PenaltyPerSlaBreach * inputs.SlaBreachesLast90Days)
            - (PenaltyPerEscalation * inputs.EscalationsLast90Days);

        score = Math.Clamp(score, 0m, 100m);

        if (inputs.OpenCases == 0 && inputs.SlaBreachesLast90Days == 0 && inputs.EscalationsLast90Days == 0)
        {
            return new DimensionResult(
                score,
                "No support pressure",
                "No open cases, SLA breaches or escalations in the last 90 days.");
        }

        var label = score >= 70m ? "Support load manageable"
            : score >= 40m ? "Support pressure building"
            : "Unresolved support cases";

        return new DimensionResult(
            score,
            label,
            $"{inputs.OpenCases} open case(s), {inputs.SlaBreachesLast90Days} SLA breach(es) and " +
            $"{inputs.EscalationsLast90Days} escalation(s) in the last 90 days.");
    }

    // ---- Relationship -----------------------------------------------------

    private static DimensionResult ScoreRelationship(HealthInputs inputs)
    {
        const decimal SingleThreadedBaseline = 35m;
        const decimal DecisionMakerCredit = 20m;
        const decimal ExecutiveCredit = 15m;
        const decimal CreditPerEngagedContact = 10m;
        const decimal MaximumBreadthCredit = 30m;

        var breadth = Math.Min(
            MaximumBreadthCredit,
            CreditPerEngagedContact * inputs.DistinctContactsEngagedLast90Days);

        var score = SingleThreadedBaseline
            + (inputs.DecisionMakerEngaged ? DecisionMakerCredit : 0m)
            + (inputs.ExecutiveRelationshipExists ? ExecutiveCredit : 0m)
            + breadth;

        score = Math.Clamp(score, 0m, 100m);

        var parts = new List<string>
        {
            $"{inputs.DistinctContactsEngagedLast90Days} contact(s) engaged in the last 90 days",
        };

        parts.Add(inputs.DecisionMakerEngaged
            ? "a decision maker is engaged"
            : "no decision-maker engagement recorded");

        if (inputs.ExecutiveRelationshipExists)
        {
            parts.Add("an executive relationship exists");
        }

        var label = score >= 75m ? "Relationship well established"
            : score >= 50m ? "Relationship developing"
            : "Relationship narrow";

        return new DimensionResult(score, label, string.Join("; ", parts) + ".");
    }

    // ---- Payment ----------------------------------------------------------

    private static DimensionResult ScorePayment(HealthInputs inputs)
    {
        if (!inputs.HasInvoiceHistory)
        {
            return new DimensionResult(
                NeutralBaseline,
                "No payment history",
                "No invoices have been issued to this customer yet.");
        }

        const decimal PenaltyPerOverdueInvoice = 25m;
        const decimal MaximumLatenessPenalty = 30m;

        var latenessPenalty = Math.Min(MaximumLatenessPenalty, Math.Max(0m, inputs.AverageInvoiceDaysLate));
        var score = Math.Clamp(
            100m - (PenaltyPerOverdueInvoice * inputs.OverdueInvoiceCount) - latenessPenalty,
            0m,
            100m);

        if (inputs.OverdueInvoiceCount == 0 && inputs.AverageInvoiceDaysLate <= 0m)
        {
            return new DimensionResult(
                score,
                "Payment behaviour stable",
                "No overdue invoices; payments have arrived on or before their due date.");
        }

        var label = score >= 70m ? "Payment behaviour acceptable"
            : score >= 40m ? "Payment behaviour slipping"
            : "Payment behaviour poor";

        return new DimensionResult(
            score,
            label,
            $"{inputs.OverdueInvoiceCount} overdue invoice(s); payments arrive on average " +
            $"{inputs.AverageInvoiceDaysLate:0.#} day(s) after the due date.");
    }

    // ---- Momentum ---------------------------------------------------------

    private static DimensionResult ScoreMomentum(HealthInputs inputs)
    {
        const decimal CreditPerStageAdvance = 10m;
        const decimal MaximumAdvanceCredit = 30m;
        const decimal PenaltyPerStalledOpportunity = 15m;
        const decimal MaximumStallPenalty = 30m;

        var advanceCredit = Math.Min(
            MaximumAdvanceCredit,
            CreditPerStageAdvance * inputs.OpportunityStageAdvancesLast90Days);

        var stallPenalty = Math.Min(
            MaximumStallPenalty,
            PenaltyPerStalledOpportunity * inputs.StalledOpportunities);

        var score = NeutralBaseline + advanceCredit - stallPenalty;

        var commitmentNote = string.Empty;

        if (inputs.CommitmentsDueLast90Days > 0)
        {
            var completionRate =
                (decimal)inputs.CommitmentsFulfilledOnTimeLast90Days / inputs.CommitmentsDueLast90Days;

            // Meeting the expected rate is neutral; beating or missing it moves
            // the score by up to 10 points either way.
            score += (completionRate - ExpectedCommitmentCompletionRate) * 50m;

            commitmentNote =
                $" {inputs.CommitmentsFulfilledOnTimeLast90Days} of {inputs.CommitmentsDueLast90Days} " +
                $"commitment(s) were met on time ({completionRate * 100m:0}%).";
        }

        score = Math.Clamp(score, 0m, 100m);

        var label = score >= 65m ? "Momentum positive"
            : score >= 45m ? "Momentum flat"
            : "Momentum stalling";

        return new DimensionResult(
            score,
            label,
            $"{inputs.OpportunityStageAdvancesLast90Days} stage advance(s) and " +
            $"{inputs.StalledOpportunities} stalled opportunity(ies) in the last 90 days.{commitmentNote}");
    }

    // ---- Explicit risk adjustments ----------------------------------------

    /// 
    /// Named risks that reduce the score beyond what the dimensions capture.
    ///
    /// These are kept separate and additive so they are individually visible in
    /// the explanation, and so removing a risk visibly restores the points.
    /// </summary>
    private static IEnumerable<HealthFactor> RiskAdjustments(HealthInputs inputs, HealthRiskPolicy policy)
    {
        if (inputs.TopContactInteractionShare is { } share
            && share.Value >= policy.ConcentrationThreshold.Value
            && inputs.DistinctContactsEngagedLast90Days > 0)
        {
            yield return new HealthFactor(
                Code: "RISK_RELATIONSHIP_CONCENTRATION",
                Dimension: HealthDimension.Risk,
                Label: "Relationship concentrated on one contact",
                Delta: -6m,
                Evidence: $"{share.Value:0.#}% of recorded commercial interaction involves a single contact. " +
                          "If that person leaves, relationship continuity is threatened.");
        }

        if (inputs.DaysToNextRenewal is { } daysToRenewal
            && daysToRenewal <= policy.RenewalHorizonDays)
        {
            if (inputs.RenewalAtRisk || !inputs.DecisionMakerEngaged)
            {
                yield return new HealthFactor(
                    Code: "RISK_RENEWAL_EXPOSURE",
                    Dimension: HealthDimension.Risk,
                    Label: "Renewal approaching without decision-maker engagement",
                    Delta: -8m,
                    Evidence: $"Renewal is {daysToRenewal} day(s) away and " +
                              (inputs.DecisionMakerEngaged
                                  ? "has been flagged as at risk."
                                  : "no decision-maker interaction has been recorded."));
            }
            else
            {
                yield return new HealthFactor(
                    Code: "RENEWAL_ENGAGED",
                    Dimension: HealthDimension.Risk,
                    Label: "Renewal active with decision maker engaged",
                    Delta: 4m,
                    Evidence: $"Renewal is {daysToRenewal} day(s) away and a decision maker is engaged.");
            }
        }

        if (inputs.SlaBreachesLast90Days >= 3)
        {
            yield return new HealthFactor(
                Code: "RISK_REPEATED_SLA_VARIANCE",
                Dimension: HealthDimension.Risk,
                Label: "Repeated service-level variance",
                Delta: -5m,
                Evidence: $"{inputs.SlaBreachesLast90Days} SLA breaches in the last 90 days indicate a pattern " +
                          "rather than an isolated miss.");
        }
    }
}
