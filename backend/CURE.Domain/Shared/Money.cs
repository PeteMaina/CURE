using System.Globalization;

namespace CURE.Domain.Shared;

public readonly record struct CurrencyCode
{
    private static readonly HashSet<string> ZeroDecimalCurrencies = new(StringComparer.Ordinal)
    {
        "BIF", "CLP", "DJF", "GNF", "ISK", "JPY", "KMF", "KRW",
        "PYG", "RWF", "UGX", "UYI", "VND", "VUV", "XAF", "XOF", "XPF",
    };

    private static readonly HashSet<string> ThreeDecimalCurrencies = new(StringComparer.Ordinal)
    {
        "BHD", "IQD", "JOD", "KWD", "LYD", "OMR", "TND",
    };

    public string Value { get; }

    private CurrencyCode(string value) => Value = value;

    public static CurrencyCode From(string? code)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            throw new DomainException(ErrorCodes.InvalidCurrency, "A currency code is required.");
        }

        var normalized = code.Trim().ToUpperInvariant();

        if (normalized.Length != 3 || !normalized.All(char.IsAsciiLetterUpper))
        {
            throw new DomainException(
                ErrorCodes.InvalidCurrency,
                "Enter a three-letter ISO 4217 currency code, for example USD or KES.",
                new Dictionary<string, object?> { ["value"] = code });
        }

        return new CurrencyCode(normalized);
    }

    public static bool TryFrom(string? code, out CurrencyCode currency)
    {
        try
        {
            currency = From(code);
            return true;
        }
        catch (DomainException)
        {
            currency = default;
            return false;
        }
    }

    public int MinorUnitDigits =>
        ZeroDecimalCurrencies.Contains(Value) ? 0
        : ThreeDecimalCurrencies.Contains(Value) ? 3
        : 2;

    public override string ToString() => Value;

    public static readonly CurrencyCode Usd = new("USD");
    public static readonly CurrencyCode Kes = new("KES");
    public static readonly CurrencyCode Eur = new("EUR");
    public static readonly CurrencyCode Gbp = new("GBP");
}


public readonly record struct Money : IComparable<Money>
{
    public decimal Amount { get; }

    public CurrencyCode Currency { get; }

    private Money(decimal amount, CurrencyCode currency)
    {
        Amount = amount;
        Currency = currency;
    }

    public static Money Of(decimal amount, CurrencyCode currency) => new(amount, currency);

    public static Money Of(decimal amount, string currency) => new(amount, CurrencyCode.From(currency));

    public static Money Zero(CurrencyCode currency) => new(0m, currency);

    public static Money NonNegative(decimal amount, CurrencyCode currency)
    {
        if (amount < 0m)
        {
            throw new DomainException(
                ErrorCodes.NegativeAmountNotAllowed,
                "This amount cannot be negative.",
                new Dictionary<string, object?> { ["amount"] = amount });
        }

        return new Money(amount, currency);
    }

    public bool IsZero => Amount == 0m;

    public bool IsNegative => Amount < 0m;

    public bool IsPositive => Amount > 0m;

    public Money Round()
        => new(Math.Round(Amount, Currency.MinorUnitDigits, MidpointRounding.ToEven), Currency);

    public Money Add(Money other)
    {
        RequireSameCurrency(other);
        return new Money(Amount + other.Amount, Currency);
    }

    public Money Subtract(Money other)
    {
        RequireSameCurrency(other);
        return new Money(Amount - other.Amount, Currency);
    }

    public Money MultiplyBy(decimal factor) => new(Amount * factor, Currency);

    public Money ApplyDiscount(Percentage discount)
        => new Money(Amount * (1m - discount.AsFraction), Currency).Round();

    public Money Negate() => new(-Amount, Currency);

    public Money Abs() => new(Math.Abs(Amount), Currency);

    public Money ConvertUsing(ExchangeRate rate)
    {
        if (rate.BaseCurrency != Currency)
        {
            throw new DomainException(
                ErrorCodes.CurrencyMismatch,
                "The exchange rate does not apply to this amount's currency.",
                new Dictionary<string, object?>
                {
                    ["amountCurrency"] = Currency.Value,
                    ["rateBaseCurrency"] = rate.BaseCurrency.Value,
                });
        }

        return new Money(Amount * rate.Rate, rate.QuoteCurrency).Round();
    }

    public static Money Sum(IEnumerable<Money> amounts, CurrencyCode currency)
    {
        var total = Zero(currency);
        foreach (var amount in amounts)
        {
            total = total.Add(amount);
        }

        return total;
    }

    private void RequireSameCurrency(Money other)
    {
        if (other.Currency != Currency)
        {
            throw new DomainException(
                ErrorCodes.CurrencyMismatch,
                "Amounts in different currencies cannot be combined without an explicit exchange rate.",
                new Dictionary<string, object?>
                {
                    ["left"] = Currency.Value,
                    ["right"] = other.Currency.Value,
                });
        }
    }

    public int CompareTo(Money other)
    {
        RequireSameCurrency(other);
        return Amount.CompareTo(other.Amount);
    }

    public override string ToString()
        => string.Create(
            CultureInfo.InvariantCulture,
            $"{Amount.ToString("N" + Currency.MinorUnitDigits, CultureInfo.InvariantCulture)} {Currency}");
}

public sealed record ExchangeRate(
    CurrencyCode BaseCurrency,
    CurrencyCode QuoteCurrency,
    decimal Rate,
    DateTimeOffset EffectiveAt,
    string Source)
{
    public static ExchangeRate Create(
        CurrencyCode baseCurrency,
        CurrencyCode quoteCurrency,
        decimal rate,
        DateTimeOffset effectiveAt,
        string source)
    {
        if (rate <= 0m)
        {
            throw DomainException.Validation("An exchange rate must be greater than 0.");
        }

        if (baseCurrency == quoteCurrency && rate != 1m)
        {
            throw DomainException.Validation("An identity exchange rate must be exactly 1.");
        }

        return new ExchangeRate(baseCurrency, quoteCurrency, rate, effectiveAt, source);
    }
}
