namespace CURE.Domain.Engagement;

/// <summary>
/// Well-known outcome constants for call and meeting activities.
///
/// Keeping these as typed constants prevents the free-text sprawl that makes
/// pipeline reporting unreliable: a rep who types "left vm" and another who
/// types "voicemail" produce the same KPI bucket when both resolve to
/// <see cref="CalledLeftVoicemail"/>.
///
/// The store writes the raw string; the domain enforces that outcome-required
/// activity types receive a non-empty value — these constants are canonical
/// suggestions surfaced by the UI, not an exhaustive closed enum.
/// </summary>
public static class ActivityOutcomes
{
    // ── Calls ─────────────────────────────────────────────────────────────────
    public const string CalledConnected       = "CALLED_CONNECTED";
    public const string CalledLeftVoicemail   = "CALLED_LEFT_VOICEMAIL";
    public const string CalledNoAnswer        = "CALLED_NO_ANSWER";
    public const string CalledBadNumber       = "CALLED_BAD_NUMBER";
    public const string CalledCallbackScheduled = "CALLED_CALLBACK_SCHEDULED";

    // ── Meetings / demos ──────────────────────────────────────────────────────
    public const string MeetingHeld           = "MEETING_HELD";
    public const string MeetingNoShow         = "MEETING_NO_SHOW";
    public const string MeetingRescheduled    = "MEETING_RESCHEDULED";
    public const string DemoCompleted         = "DEMO_COMPLETED";
    public const string DemoFollowUpRequired  = "DEMO_FOLLOW_UP_REQUIRED";

    // ── Tasks ─────────────────────────────────────────────────────────────────
    public const string TaskDone              = "TASK_DONE";
    public const string TaskDeferred          = "TASK_DEFERRED";

    // ── Emails ────────────────────────────────────────────────────────────────
    public const string EmailSent             = "EMAIL_SENT";
    public const string EmailBounced          = "EMAIL_BOUNCED";
    public const string EmailReplied          = "EMAIL_REPLIED";
}
