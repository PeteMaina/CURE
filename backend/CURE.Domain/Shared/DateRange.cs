namespace CURE.Domain.Shared;

/// <summary>
/// A half-open interval of instants: [Start, End).
///
/// Half-open is the right default for reporting periods — adjacent ranges tile
/// without double-counting the boundary instant, so a deal closing exactly at
/// midnight lands in exactly one quarter.
/// </summary>
public readonly record struct DateRange
{
    public DateTimeOffset Start { get; }

    public DateTimeOffset End { get; }

    private DateRange(DateTimeOffset start, DateTimeOffset end)
    {
        Start = start;
        End = end;
    }

    public static DateRange Create(DateTimeOffset start, DateTimeOffset end)
    {
        if (end < start)
        {
            throw new DomainException(
                ErrorCodes.InvalidDateRange,
                "The end of the period cannot fall before its start.",
                new Dictionary<string, object?>
                {
                    ["start"] = start,
                    ["end"] = end,
                });
        }

        return new DateRange(start, end);
    }

    public static DateRange TrailingDays(DateTimeOffset asOf, int days)
    {
        if (days <= 0)
        {
            throw DomainException.Validation("A trailing window must span at least one day.");
        }

        return new DateRange(asOf.AddDays(-days), asOf);
    }

    public TimeSpan Duration => End - Start;

    public bool Contains(DateTimeOffset instant) => instant >= Start && instant < End;

    public bool Overlaps(DateRange other) => Start < other.End && other.Start < End;

    public override string ToString() => $"{Start:O} .. {End:O}";
}
