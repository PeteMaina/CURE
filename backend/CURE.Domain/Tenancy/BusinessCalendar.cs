using CURE.Domain.Shared;

namespace CURE.Domain.Tenancy;

/// <summary>A span of working time on a given weekday, in the calendar's local time.</summary>
public sealed record WorkingWindow(DayOfWeek Day, TimeOnly Start, TimeOnly End)
{
    public static WorkingWindow Create(DayOfWeek day, TimeOnly start, TimeOnly end)
    {
        if (end <= start)
        {
            throw DomainException.Validation(
                $"Working hours for {day} must end after they start.");
        }

        return new WorkingWindow(day, start, end);
    }

    public TimeSpan Duration => End - Start;
}

/// <summary>A non-working date, with the reason shown in SLA explanations.</summary>
public sealed record Holiday(DateOnly Date, string Name);

/// <summary>
/// A tenant's working calendar: time zone, working days, working
/// hours and holidays.
///
/// This type exists so that no part of CURE ever computes a deadline as
/// <c>DateTime.Now + X hours</c> . A support
/// promise of "4 business hours" made at 16:00 on the Friday before a public
/// holiday is due mid-morning on Tuesday, and this class is the only place that
/// knows how to work that out.
///
/// Daylight-saving handling: arithmetic is performed in the calendar's local
/// wall-clock time and converted to instants at the boundaries. Local times that
/// do not exist (spring-forward gap) advance to the first valid instant;
/// ambiguous local times (autumn fall-back) resolve to their first occurrence.
/// </summary>
public sealed class BusinessCalendar
{
    /// <summary>
    ///  A "24x7" calendar resolves in one or
    /// two iterations; a sparse calendar (one 2-hour window per week) still
    /// resolves well inside this. Hitting the bound means the calendar is
    /// unsatisfiable, which is reported rather than looping forever.
    
    private const int MaxDaysToScan = 4000;

    private readonly Dictionary<DayOfWeek, List<WorkingWindow>> _windowsByDay;
    private readonly Dictionary<DateOnly, Holiday> _holidays;

    private BusinessCalendar(
        string timeZoneId,
        TimeZoneInfo timeZone,
        Dictionary<DayOfWeek, List<WorkingWindow>> windowsByDay,
        Dictionary<DateOnly, Holiday> holidays)
    {
        TimeZoneId = timeZoneId;
        TimeZone = timeZone;
        _windowsByDay = windowsByDay;
        _holidays = holidays;
    }

    public string TimeZoneId { get; }

    public TimeZoneInfo TimeZone { get; }

    public IReadOnlyCollection<Holiday> Holidays => _holidays.Values;

    public static BusinessCalendar Create(
        string timeZoneId,
        IEnumerable<WorkingWindow> windows,
        IEnumerable<Holiday>? holidays = null)
    {
        TimeZoneInfo timeZone;

        try
        {
            timeZone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
        }
        catch (Exception exception) when (
            exception is TimeZoneNotFoundException or InvalidTimeZoneException)
        {
            throw new DomainException(
                ErrorCodes.BusinessCalendarNotFound,
                "The configured time zone is not recognised on this system.",
                new Dictionary<string, object?> { ["timeZoneId"] = timeZoneId });
        }

        var byDay = new Dictionary<DayOfWeek, List<WorkingWindow>>();

        foreach (var window in windows)
        {
            if (!byDay.TryGetValue(window.Day, out var list))
            {
                list = new List<WorkingWindow>();
                byDay[window.Day] = list;
            }

            list.Add(window);
        }

        foreach (var (day, list) in byDay)
        {
            list.Sort((left, right) => left.Start.CompareTo(right.Start));

            
            for (var index = 1; index < list.Count; index++)
            {
                if (list[index].Start < list[index - 1].End)
                {
                    throw DomainException.Validation(
                        $"Working hours for {day} overlap. Each window must be distinct.");
                }
            }
        }

        if (byDay.Count == 0 || byDay.Values.All(list => list.Count == 0))
        {
            throw new DomainException(
                ErrorCodes.BusinessCalendarHasNoWorkingTime,
                "A business calendar must define at least one working window.");
        }

        var holidayMap = (holidays ?? Array.Empty<Holiday>())
            .GroupBy(holiday => holiday.Date)
            .ToDictionary(group => group.Key, group => group.First());

        return new BusinessCalendar(timeZoneId, timeZone, byDay, holidayMap);
    }

    /// <summary>
    /// A continuous calendar: every instant counts. Used for policies that
    /// explicitly promise round-the-clock response, and as the calendar for
    /// severity-1 incident SLAs.
    /// </summary>
    public static BusinessCalendar Continuous(string timeZoneId)
    {
        var windows = Enum.GetValues<DayOfWeek>()
            .Select(day => new WorkingWindow(day, TimeOnly.MinValue, new TimeOnly(23, 59, 59, 999)));

        return Create(timeZoneId, windows);
    }

    public bool IsHoliday(DateOnly date) => _holidays.ContainsKey(date);

    public Holiday? HolidayOn(DateOnly date)
        => _holidays.TryGetValue(date, out var holiday) ? holiday : null;

    /// <summary>Whether the given instant falls inside a working window.</summary>
    public bool IsWorkingTime(DateTimeOffset instant)
    {
        var local = ToLocal(instant);
        var date = DateOnly.FromDateTime(local);
        var time = TimeOnly.FromDateTime(local);

        return WindowsOn(date).Any(window => time >= window.Start && time < window.End);
    }

 
    public DateTimeOffset NextWorkingInstant(DateTimeOffset instant)
    {
        var local = ToLocal(instant);
        var date = DateOnly.FromDateTime(local);
        var time = TimeOnly.FromDateTime(local);

        for (var dayOffset = 0; dayOffset < MaxDaysToScan; dayOffset++)
        {
            var scanDate = date.AddDays(dayOffset);
            var cursor = dayOffset == 0 ? time : TimeOnly.MinValue;

            foreach (var window in WindowsOn(scanDate))
            {
                if (cursor < window.End)
                {
                    var start = cursor >= window.Start ? cursor : window.Start;
                    return ToInstant(scanDate, start);
                }
            }
        }

        throw new DomainException(
            ErrorCodes.BusinessCalendarHasNoWorkingTime,
            "No working time was found within a reasonable horizon for this calendar.");
    }

    /// <summary>
   
    /// This is the primitive behind every SLA deadline. Time outside working
    /// windows, and whole holidays, are skipped rather than consumed.
    
    public DateTimeOffset AddWorkingTime(DateTimeOffset start, TimeSpan workingDuration)
    {
        if (workingDuration < TimeSpan.Zero)
        {
            throw DomainException.Validation("Working time to add cannot be negative.");
        }

        if (workingDuration == TimeSpan.Zero)
        {
            return NextWorkingInstant(start);
        }

        var local = ToLocal(start);
        var date = DateOnly.FromDateTime(local);
        var time = TimeOnly.FromDateTime(local);
        var remaining = workingDuration;

        for (var dayOffset = 0; dayOffset < MaxDaysToScan; dayOffset++)
        {
            var scanDate = date.AddDays(dayOffset);
            var cursor = dayOffset == 0 ? time : TimeOnly.MinValue;

            foreach (var window in WindowsOn(scanDate))
            {
                if (cursor >= window.End)
                {
                    continue;
                }

                var from = cursor >= window.Start ? cursor : window.Start;
                var available = window.End - from;

                if (available >= remaining)
                {
                    return ToInstant(scanDate, from.Add(remaining));
                }

                remaining -= available;
            }
        }

        throw new DomainException(
            ErrorCodes.BusinessCalendarHasNoWorkingTime,
            "The requested working duration exceeds this calendar's horizon.");
    }

    /// <summary>
    /// Working time elapsed between two instants. Used to measure actual response
    /// and resolution time against a promise 
    public TimeSpan WorkingTimeBetween(DateTimeOffset from, DateTimeOffset to)
    {
        if (to <= from)
        {
            return TimeSpan.Zero;
        }

        var localFrom = ToLocal(from);
        var localTo = ToLocal(to);

        var startDate = DateOnly.FromDateTime(localFrom);
        var endDate = DateOnly.FromDateTime(localTo);
        var startTime = TimeOnly.FromDateTime(localFrom);
        var endTime = TimeOnly.FromDateTime(localTo);

        var total = TimeSpan.Zero;
        var dayCount = endDate.DayNumber - startDate.DayNumber;

        if (dayCount > MaxDaysToScan)
        {
            dayCount = MaxDaysToScan;
        }

        for (var dayOffset = 0; dayOffset <= dayCount; dayOffset++)
        {
            var scanDate = startDate.AddDays(dayOffset);
            var lowerBound = dayOffset == 0 ? startTime : TimeOnly.MinValue;
            var upperBound = scanDate == endDate ? endTime : TimeOnly.MaxValue;

            foreach (var window in WindowsOn(scanDate))
            {
                var overlapStart = lowerBound > window.Start ? lowerBound : window.Start;
                var overlapEnd = upperBound < window.End ? upperBound : window.End;

                if (overlapEnd > overlapStart)
                {
                    total += overlapEnd - overlapStart;
                }
            }
        }

        return total;
    }

    private IReadOnlyList<WorkingWindow> WindowsOn(DateOnly date)
    {
        if (IsHoliday(date))
        {
            return Array.Empty<WorkingWindow>();
        }

        return _windowsByDay.TryGetValue(date.DayOfWeek, out var windows)
            ? windows
            : Array.Empty<WorkingWindow>();
    }

    private DateTime ToLocal(DateTimeOffset instant)
        => TimeZoneInfo.ConvertTime(instant, TimeZone).DateTime;

    
    private DateTimeOffset ToInstant(DateOnly date, TimeOnly time)
    {
        var local = date.ToDateTime(time, DateTimeKind.Unspecified);

        // Spring forward: this wall-clock time never occurs. Advance to the first
        // instant that does. The gap is at most a couple of hours in every zone
        // in the IANA database.
        var guard = 0;
        while (TimeZone.IsInvalidTime(local) && guard++ < 240)
        {
            local = local.AddMinutes(1);
        }

        if (TimeZone.IsAmbiguousTime(local))
        {
            // Autumn fall-back: the wall-clock time occurs twice. Take the first
            // occurrence, which is the one with the larger UTC offset.
            var offsets = TimeZone.GetAmbiguousTimeOffsets(local);
            var firstOccurrence = offsets.Max();
            return new DateTimeOffset(local, firstOccurrence);
        }

        return new DateTimeOffset(local, TimeZone.GetUtcOffset(local));
    }
}
