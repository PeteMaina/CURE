using System.Text;
using System.Text.RegularExpressions;

namespace CURE.Domain.Shared;

/// <summary>
/// A telephone number in E.164 form.
///
/// A default country dialling code is required at parse time so that a locally
/// formatted number ("0712 345 678") can be normalized without the domain
/// guessing a country. Error messages name the expected format for the tenant's
/// region rather than saying "Invalid input" 
/// </summary>
public readonly partial record struct PhoneNumber
{
    [GeneratedRegex(@"^\+[1-9]\d{6,14}$", RegexOptions.CultureInvariant)]
    private static partial Regex E164Pattern();

    public string Value { get; }

    private PhoneNumber(string value) => Value = value;

    /// Parses a number that is already in international form.
    /// 
    public static PhoneNumber From(string? value)
    {
        var candidate = Strip(value);

        if (candidate.Length == 0)
        {
            throw new DomainException(ErrorCodes.InvalidPhone, "A phone number is required.");
        }

        if (!E164Pattern().IsMatch(candidate))
        {
            throw new DomainException(
                ErrorCodes.InvalidPhone,
                "Enter the number in international format, beginning with + and the country code.",
                new Dictionary<string, object?> { ["value"] = value });
        }

        return new PhoneNumber(candidate);
    }

    /// <summary>
    /// Parses a number that may be written in local form, promoting it using the
    /// supplied dialling code.
    ///
    /// Example: <c>FromLocal("0712345678", "+254")</c> yields
    /// <c>+254712345678</c>, and the error message for a bad value names that
    /// expected shape — "Enter a valid phone number beginning with +254"
    /// 
    /// </summary>
    public static PhoneNumber FromLocal(string? value, string defaultDiallingCode)
    {
        var dialling = Strip(defaultDiallingCode);

        if (!dialling.StartsWith('+') || dialling.Length < 2)
        {
            throw DomainException.Validation(
                "The tenant's default dialling code is not configured correctly.");
        }

        var candidate = Strip(value);

        if (candidate.Length == 0)
        {
            throw new DomainException(ErrorCodes.InvalidPhone, "A phone number is required.");
        }

        // Already international.
        if (candidate.StartsWith('+'))
        {
            return From(candidate);
        }

        // Trunk prefix: a single leading zero is dropped when promoting.
        var national = candidate.TrimStart('0');

        if (national.Length == 0)
        {
            throw new DomainException(
                ErrorCodes.InvalidPhone,
                $"Enter a valid phone number beginning with {dialling}.",
                new Dictionary<string, object?> { ["value"] = value });
        }

        var promoted = dialling + national;

        if (!E164Pattern().IsMatch(promoted))
        {
            throw new DomainException(
                ErrorCodes.InvalidPhone,
                $"Enter a valid phone number beginning with {dialling}.",
                new Dictionary<string, object?> { ["value"] = value });
        }

        return new PhoneNumber(promoted);
    }

    public static PhoneNumber? FromOptional(string? value, string defaultDiallingCode)
        => string.IsNullOrWhiteSpace(value) ? null : FromLocal(value, defaultDiallingCode);

    /// <summary>
    /// Digits only, for duplicate matching . Formatting differences
    /// between "+254 712 345 678" and "+254712345678" must not hide a duplicate.
    /// </summary>
    public string Normalized => new(Value.Where(char.IsAsciiDigit).ToArray());

    private static string Strip(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return string.Empty;
        }

        var builder = new StringBuilder(raw.Length);

        foreach (var character in raw)
        {
            if (character == '+' && builder.Length == 0)
            {
                builder.Append('+');
            }
            else if (char.IsAsciiDigit(character))
            {
                builder.Append(character);
            }

            // Spaces, hyphens, brackets and dots are formatting noise: dropped.
        }

        return builder.ToString();
    }

    public override string ToString() => Value;
}
