namespace CURE.Domain.Engagement;

/// <summary>
/// Lifecycle state of a single <see cref="Activity"/> aggregate.
///
/// Only terminal states (Completed, Cancelled) may not transition further;
/// all forward transitions are enforced by the aggregate itself so that no
/// caller can accidentally skip a step or resurrect a closed activity.
/// </summary>
public enum ActivityStatus
{
    Planned    = 1,
    InProgress = 2,
    Completed  = 3,
    Cancelled  = 4,
}

public static class ActivityStatusExtensions
{
    /// <summary>
    /// Returns true when no further state changes are permitted.
    /// Used by the aggregate guard to short-circuit illegal transitions early.
    /// </summary>
    public static bool IsTerminal(this ActivityStatus status)
        => status is ActivityStatus.Completed or ActivityStatus.Cancelled;

    public static string Label(this ActivityStatus status) => status switch
    {
        ActivityStatus.Planned    => "Planned",
        ActivityStatus.InProgress => "In progress",
        ActivityStatus.Completed  => "Completed",
        ActivityStatus.Cancelled  => "Cancelled",
        _                         => status.ToString(),
    };
}
