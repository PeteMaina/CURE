using System.Text.RegularExpressions;

namespace CURE.Domain.Shared;

/// <summary>
/// A syntactically valid email address, stored alongside a normalized form.

public readonly partial record struct EmailAddress
{
    // tightenup
    [GeneratedRegex(
        @"^[^@\s]{1,64}@(?:[A-Za-z0-9](?:[A-Za-z0-9-]{0,61}[A-Za-z0-9])?\.)+[A-Za-z]{2,63}$",
        RegexOptions.CultureInvariant)]
    private static partial Regex Pattern();

    public string Value { get; }

    private EmailAddress(string value) => Value = value;

    public static EmailAddress From(string? value)
    {
        var candidate = value?.Trim() ?? string.Empty;

        if (candidate.Length == 0)
        {
            throw new DomainException(ErrorCodes.InvalidEmail, "An email address is required.");
        }

        if (candidate.Length > 254 || !Pattern().IsMatch(candidate))
        {
            throw new DomainException(
                ErrorCodes.InvalidEmail,
                "Enter a valid email address, for example peter@gmail.com.",
                new Dictionary<string, object?> { ["value"] = candidate });
        }

        return new EmailAddress(candidate);
    }

    public static EmailAddress? FromOptional(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : From(value);

    public static bool TryFrom(string? value, out EmailAddress email)
    {
        try
        {
            email = From(value);
            return true;
        }
        catch (DomainException)
        {
            email = default;
            return false;
        }
    }

    /// <summary>Matching key for duplicate detection. 
    public string Normalized => Value.ToLowerInvariant();

    /// <summary>
    /// The domain part, used to associate a contact with an organization's web
    /// domain during duplicate analysis.
    
    public string Domain => Value[(Value.IndexOf('@') + 1)..].ToLowerInvariant();

    public override string ToString() => Value;
}
