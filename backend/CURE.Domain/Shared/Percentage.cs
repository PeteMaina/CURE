namespace CURE.Domain.Shared;

/// A percentage expressed on a 0–100 scale.

public readonly record struct Percentage : IComparable<Percentage>
{
    public decimal Value { get; }

    private Percentage(decimal value) => Value = value;

    public static Percentage From(decimal value)
    {
        if (value < 0m || value > 100m)
        {
            throw new DomainException(
                ErrorCodes.InvalidPercentage,
                "Enter a percentage between 0 and 100.",
                new Dictionary<string, object?> { ["value"] = value });
        }

        return new Percentage(value);
    }

    public static Percentage Clamp(decimal value) => new(Math.Clamp(value, 0m, 100m));

    public static readonly Percentage Zero = new(0m);

    public static readonly Percentage OneHundred = new(100m);

    /// 0.0–1.0, for multiplication against amounts.
    public decimal AsFraction => Value / 100m;

    public int CompareTo(Percentage other) => Value.CompareTo(other.Value);

    public override string ToString() => $"{Value:0.##}%";
}
