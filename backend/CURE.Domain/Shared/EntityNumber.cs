using System.Globalization;
using System.Text.RegularExpressions;

namespace CURE.Domain.Shared;

/// A human-readable record reference such as CUS-000042
///
/// These are display and support aids, unique per tenant. They are NOT the
/// public API identifier
public readonly partial record struct EntityNumber
{
    [GeneratedRegex(@"^[A-Z]{2,4}-\d{6,}$", RegexOptions.CultureInvariant)]
    private static partial Regex Pattern();

    public string Value { get; }

    private EntityNumber(string value) => Value = value;

    public static EntityNumber From(string? value)
    {
        var candidate = value?.Trim().ToUpperInvariant() ?? string.Empty;

        if (!Pattern().IsMatch(candidate))
        {
            throw new DomainException(
                ErrorCodes.InvalidEntityNumber,
                "A record reference looks like CUS-000042.",
                new Dictionary<string, object?> { ["value"] = value });
        }

        return new EntityNumber(candidate);
    }

    /// Formats an allocated sequence value. Six digits is the minimum width;
    /// beyond a million records the number simply grows rather than wrapping or
    /// colliding.
    
    public static EntityNumber Format(string prefix, long sequenceValue)
    {
        if (string.IsNullOrWhiteSpace(prefix))
        {
            throw DomainException.Validation("A record-reference prefix is required.");
        }

        if (sequenceValue <= 0)
        {
            throw DomainException.Validation("A record-reference sequence value must be positive.");
        }

        var normalizedPrefix = prefix.Trim().ToUpperInvariant();
        var digits = sequenceValue.ToString(CultureInfo.InvariantCulture).PadLeft(6, '0');

        return From($"{normalizedPrefix}-{digits}");
    }

    public string Prefix => Value[..Value.IndexOf('-')];

    public override string ToString() => Value;
}


/// The registered reference prefixes. Centralized so a new entity type cannot
/// quietly reuse an existing prefix 
public static class NumberPrefixes
{
    public const string Customer = "CUS";
    public const string Contact = "CON";
    public const string Lead = "LED";
    public const string Opportunity = "OPP";
    public const string Case = "CAS";
    public const string Contract = "CTR";
    public const string Invoice = "INV";
    public const string Activity = "ACT";
    public const string Task = "TSK";
    public const string Approval = "APR";
    public const string Commitment = "CMT";
    public const string Export = "EXP";
    public const string Import = "IMP";
}
