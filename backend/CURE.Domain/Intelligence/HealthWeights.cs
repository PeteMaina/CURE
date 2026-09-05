using CURE.Domain.Shared;

namespace CURE.Domain.Intelligence;

/// The explainable dimensions of relationship health (CURE.md 15).
public enum HealthDimension
{
    Engagement = 0,
    Revenue = 1,
    Support = 2,
    Relationship = 3,
    Payment = 4,
    Momentum = 5,

    /// Explicit downward adjustments from detected risk, kept separate so they are visible.
    Risk = 6,
}

/// 
/// Relative emphasis of each health dimension for a tenant.
///
/// Weights are normalised, so an administrator can enter 25/20/20/15/10/10 or
/// 5/4/4/3/2/2 and get the same result. That means "make revenue matter more"
/// is a one-field change and cannot accidentally break the 0–100 bound.
/// 
public sealed record HealthWeights
{
    private HealthWeights(
        decimal engagement,
        decimal revenue,
        decimal support,
        decimal relationship,
        decimal payment,
        decimal momentum)
    {
        Engagement = engagement;
        Revenue = revenue;
        Support = support;
        Relationship = relationship;
        Payment = payment;
        Momentum = momentum;
    }

    public decimal Engagement { get; }

    public decimal Revenue { get; }

    public decimal Support { get; }

    public decimal Relationship { get; }

    public decimal Payment { get; }

    public decimal Momentum { get; }

    public static HealthWeights Create(
        decimal engagement,
        decimal revenue,
        decimal support,
        decimal relationship,
        decimal payment,
        decimal momentum)
    {
        var all = new[] { engagement, revenue, support, relationship, payment, momentum };

        if (all.Any(weight => weight < 0m))
        {
            throw new DomainException(
                ErrorCodes.HealthWeightsInvalid,
                "Health dimension weights cannot be negative.");
        }

        if (all.Sum() <= 0m)
        {
            throw new DomainException(
                ErrorCodes.HealthWeightsInvalid,
                "At least one health dimension must carry weight.");
        }

        return new HealthWeights(engagement, revenue, support, relationship, payment, momentum);
    }

    public decimal Total => Engagement + Revenue + Support + Relationship + Payment + Momentum;

    /// The weight of one dimension as a 0–1 share of the total.
    public decimal ShareOf(HealthDimension dimension) => dimension switch
    {
        HealthDimension.Engagement => Engagement / Total,
        HealthDimension.Revenue => Revenue / Total,
        HealthDimension.Support => Support / Total,
        HealthDimension.Relationship => Relationship / Total,
        HealthDimension.Payment => Payment / Total,
        HealthDimension.Momentum => Momentum / Total,
        HealthDimension.Risk => 0m,
        _ => 0m,
    };

    /// 
    /// Identifies the weighting used for a given snapshot, so a score computed
    /// last month can be understood even after an administrator retunes the
    /// weights (CURE.md 12, 140).
    /// 
    public string Fingerprint =>
        $"{Engagement:0.##}/{Revenue:0.##}/{Support:0.##}/{Relationship:0.##}/{Payment:0.##}/{Momentum:0.##}";
}
