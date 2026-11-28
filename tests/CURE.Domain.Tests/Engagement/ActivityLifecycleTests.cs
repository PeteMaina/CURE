namespace CURE.Domain.Tests.Engagement;

using CURE.Domain.Engagement;
using CURE.Domain.Engagement.Events;
using CURE.Domain.Shared;

/// <summary>
/// Pure domain tests — no database, no network, no framework bootstrapping.
/// Every assertion exercises a business rule that lives exclusively in the
/// Activity aggregate, ensuring the domain can be reasoned about in isolation.
/// </summary>
public sealed class ActivityLifecycleTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid ActorId  = Guid.NewGuid();
    private static readonly DateTimeOffset Now = DateTimeOffset.UtcNow;

    // ── Logging ───────────────────────────────────────────────────────────────

    [Fact]
    public void Log_ValidCallActivity_CreatesInPlannedStatus()
    {
        var activity = LogCall("Follow-up with Acme procurement");

        Assert.Equal(ActivityStatus.Planned, activity.Status);
        Assert.Equal(ActivityType.Call,      activity.Type);
        Assert.Equal("Follow-up with Acme procurement", activity.Subject);
        Assert.Null(activity.Outcome);
        Assert.Null(activity.CompletedAt);
    }

    [Fact]
    public void Log_ValidCallActivity_RaisesActivityLoggedEvent()
    {
        var activity = LogCall("Demo call");

        var evt = Assert.Single(activity.DomainEvents.OfType<ActivityLoggedDomainEvent>());
        Assert.Equal(ActivityType.Call, evt.Type);
        Assert.Equal("Demo call",       evt.Subject);
        Assert.Equal(TenantId,          evt.TenantId);
    }

    [Fact]
    public void Log_EmptySubject_ThrowsDomainException()
    {
        var ex = Assert.Throws<DomainException>(() =>
            Activity.Log(TenantId, ActivityType.Call, "   ", null, null, null,
                         null, null, null, ActorId, ActorId, Now));

        Assert.Equal(ErrorCodes.ValidationFailed, ex.Code);
    }

    [Fact]
    public void Log_NoteType_DefaultDurationIsNull()
    {
        // Notes have no meaningful calendar duration.
        var activity = Activity.Log(TenantId, ActivityType.Note, "Internal note", null, null, null,
                                    null, null, null, ActorId, ActorId, Now);

        Assert.Null(activity.DurationMinutes);
    }

    [Fact]
    public void Log_MeetingType_DefaultDurationIsPopulated()
    {
        var activity = Activity.Log(TenantId, ActivityType.Meeting, "Kick-off call", null, null, null,
                                    null, null, null, ActorId, ActorId, Now);

        Assert.NotNull(activity.DurationMinutes);
        Assert.True(activity.DurationMinutes! > 0);
    }

    // ── Completion ────────────────────────────────────────────────────────────

    [Fact]
    public void Complete_CallWithOutcome_TransitionsToCompleted()
    {
        var activity = LogCall("Qualification call");

        activity.Complete(ActivityOutcomes.CalledConnected, null, null, ActorId, Now.AddMinutes(10));

        Assert.Equal(ActivityStatus.Completed, activity.Status);
        Assert.Equal(ActivityOutcomes.CalledConnected, activity.Outcome);
        Assert.NotNull(activity.CompletedAt);
    }

    [Fact]
    public void Complete_CallWithOutcome_RaisesActivityCompletedEvent()
    {
        var activity = LogCall("Nurture call");
        activity.ClearDomainEvents();

        activity.Complete(ActivityOutcomes.CalledLeftVoicemail, 8, null, ActorId, Now.AddMinutes(5));

        var evt = Assert.Single(activity.DomainEvents.OfType<ActivityCompletedDomainEvent>());
        Assert.Equal(ActivityOutcomes.CalledLeftVoicemail, evt.Outcome);
    }

    [Fact]
    public void Complete_CallWithoutOutcome_ThrowsActivityOutcomeRequired()
    {
        var activity = LogCall("Cold call");

        var ex = Assert.Throws<DomainException>(() =>
            activity.Complete(null, null, null, ActorId, Now.AddMinutes(2)));

        Assert.Equal(ErrorCodes.ActivityOutcomeRequired, ex.Code);
    }

    [Fact]
    public void Complete_NoteWithoutOutcome_Succeeds_BecauseNotesNeedNoOutcome()
    {
        var note = Activity.Log(TenantId, ActivityType.Note, "Post-meeting note", "Details", null, null,
                                null, null, null, ActorId, ActorId, Now);

        note.Complete(null, null, null, ActorId, Now.AddMinutes(1));

        Assert.Equal(ActivityStatus.Completed, note.Status);
    }

    [Fact]
    public void Complete_AlreadyCompleted_ThrowsInvalidStatusTransition()
    {
        var activity = LogCall("Demo");
        activity.Complete(ActivityOutcomes.CalledConnected, null, null, ActorId, Now.AddMinutes(5));
        activity.ClearDomainEvents();

        var ex = Assert.Throws<DomainException>(() =>
            activity.Complete(ActivityOutcomes.CalledConnected, null, null, ActorId, Now.AddMinutes(10)));

        Assert.Equal(ErrorCodes.ActivityInvalidStatusTransition, ex.Code);
    }

    // ── Cancellation ──────────────────────────────────────────────────────────

    [Fact]
    public void Cancel_PlannedActivity_TransitionsToCancelled()
    {
        var activity = LogCall("No-show follow-up");

        activity.Cancel("Contact unreachable", ActorId, Now.AddMinutes(30));

        Assert.Equal(ActivityStatus.Cancelled, activity.Status);
    }

    [Fact]
    public void Cancel_PlannedActivity_RaisesActivityCancelledEvent()
    {
        var activity = LogCall("Cancelled demo");
        activity.ClearDomainEvents();

        activity.Cancel("Prospect went dark", ActorId, Now.AddMinutes(30));

        var evt = Assert.Single(activity.DomainEvents.OfType<ActivityCancelledDomainEvent>());
        Assert.Equal("Prospect went dark", evt.Reason);
    }

    [Fact]
    public void Cancel_AlreadyCancelledActivity_ThrowsInvalidStatusTransition()
    {
        var activity = LogCall("Old activity");
        activity.Cancel("No show", ActorId, Now.AddMinutes(10));
        activity.ClearDomainEvents();

        var ex = Assert.Throws<DomainException>(() =>
            activity.Cancel("Double cancel", ActorId, Now.AddMinutes(20)));

        Assert.Equal(ErrorCodes.ActivityInvalidStatusTransition, ex.Code);
    }

    [Fact]
    public void Cancel_CompletedActivity_ThrowsInvalidStatusTransition()
    {
        var activity = LogCall("Completed call");
        activity.Complete(ActivityOutcomes.CalledConnected, null, null, ActorId, Now.AddMinutes(5));
        activity.ClearDomainEvents();

        var ex = Assert.Throws<DomainException>(() =>
            activity.Cancel("Too late", ActorId, Now.AddMinutes(10)));

        Assert.Equal(ErrorCodes.ActivityInvalidStatusTransition, ex.Code);
    }

    // ── Tenant guard ──────────────────────────────────────────────────────────

    [Fact]
    public void AssertBelongsToTenant_WrongTenant_ThrowsCrossTenantAccessDenied()
    {
        var activity = LogCall("Cross-tenant attempt");
        var wrongTenant = Guid.NewGuid();

        var ex = Assert.Throws<DomainException>(() => activity.AssertBelongsToTenant(wrongTenant));
        Assert.Equal(ErrorCodes.CrossTenantAccessDenied, ex.Code);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static Activity LogCall(string subject)
        => Activity.Log(TenantId, ActivityType.Call, subject, null,
                        DateTimeOffset.UtcNow.AddDays(1), null,
                        Guid.NewGuid(), null, null, ActorId, ActorId, Now);
}
