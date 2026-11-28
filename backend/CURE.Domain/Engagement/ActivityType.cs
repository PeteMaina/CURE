namespace CURE.Domain.Engagement;

/// <summary>
/// The nature of the customer-facing interaction being recorded.
///
/// Every type carries the metadata the system needs to enforce completion
/// rules (outcome required), suggest a sensible default block on the
/// representative's calendar (default duration), and display a human-readable
/// label without a switch statement scattered across five call-sites.
/// </summary>
public enum ActivityType
{
    Call = 1,
    Email = 2,
    Meeting = 3,
    Note = 4,
    Task = 5,
    Demo = 6,
    LinkedInMessage = 7,
    SmsMessage = 8,
}

public static class ActivityTypeMetadata
{
    private static readonly Dictionary<ActivityType, (string Label, int DefaultDurationMinutes, bool OutcomeRequired)> _map = new()
    {
        [ActivityType.Call]            = ("Phone call",        15,  true),
        [ActivityType.Email]           = ("Email",              0, false),
        [ActivityType.Meeting]         = ("Meeting",           60,  true),
        [ActivityType.Note]            = ("Note",               0, false),
        [ActivityType.Task]            = ("Task",               0,  true),
        [ActivityType.Demo]            = ("Product demo",      60,  true),
        [ActivityType.LinkedInMessage] = ("LinkedIn message",   0, false),
        [ActivityType.SmsMessage]      = ("SMS",                0, false),
    };

    public static string Label(this ActivityType type)
        => _map.TryGetValue(type, out var m) ? m.Label : type.ToString();

    /// <summary>How long to block on the rep's calendar when scheduling this type.</summary>
    public static int DefaultDurationMinutes(this ActivityType type)
        => _map.TryGetValue(type, out var m) ? m.DefaultDurationMinutes : 0;

    /// <summary>
    /// Whether the system must refuse to mark this activity complete
    /// until the rep records a non-empty outcome string.
    /// </summary>
    public static bool OutcomeRequired(this ActivityType type)
        => _map.TryGetValue(type, out var m) && m.OutcomeRequired;
}
