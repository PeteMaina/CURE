namespace CURE.Domain.Tests.Pipeline;

using CURE.Domain.Pipeline;
using CURE.Domain.Pipeline.Events;
using CURE.Domain.Shared;
using Xunit;

public sealed class LeadLifecycleTests
{
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _actorId = Guid.NewGuid();
    private readonly DateTimeOffset _now = DateTimeOffset.UtcNow;
    private readonly EntityNumber _leadNumber = EntityNumber.Format(NumberPrefixes.Lead, 501);

    [Fact]
    public void Create_InitializesAsNewAndEmitsEvent()
    {
        var lead = Lead.Create(
            Guid.NewGuid(),
            _tenantId,
            _leadNumber,
            "Sarah",
            "Connor",
            "Cyberdyne Systems",
            EmailAddress.From("sconnor@cyberdyne.io"),
            null,
            "Inbound Web",
            _actorId,
            _actorId,
            _now);

        Assert.Equal(LeadStatus.New, lead.Status);
        Assert.Equal("Sarah Connor", lead.FullName);
        Assert.Single(lead.DomainEvents);
        Assert.IsType<LeadCreatedDomainEvent>(lead.DomainEvents[0]);
    }

    [Fact]
    public void Qualify_AdvancesStatusThroughProgressiveFunnel()
    {
        var lead = Lead.Create(
            Guid.NewGuid(), _tenantId, _leadNumber, "John", "Doe", "Acme", null, null, null, null, _actorId, _now);

        lead.MarkContacted(_actorId, _now.AddHours(2));
        Assert.Equal(LeadStatus.Contacted, lead.Status);

        lead.MarkEngaged(_actorId, _now.AddHours(5));
        Assert.Equal(LeadStatus.Engaged, lead.Status);

        lead.Qualify(_actorId, _now.AddDays(1));
        Assert.Equal(LeadStatus.Qualified, lead.Status);
        Assert.True(lead.Status.CanConvert());
    }

    [Fact]
    public void Convert_RequiresQualifiedStatus()
    {
        var lead = Lead.Create(
            Guid.NewGuid(), _tenantId, _leadNumber, "Jane", "Smith", "GlobalTech", null, null, null, null, _actorId, _now);

        var ex = Assert.Throws<DomainException>(() =>
            lead.Convert(_actorId, _now.AddDays(1), Guid.NewGuid(), null, null));

        Assert.Equal(ErrorCodes.LeadNotQualified, ex.Code);
    }

    [Fact]
    public void Convert_SuccessfulConversionEmitsEventAndLocksLead()
    {
        var lead = Lead.Create(
            Guid.NewGuid(), _tenantId, _leadNumber, "Jane", "Smith", "GlobalTech", null, null, null, null, _actorId, _now);

        lead.Qualify(_actorId, _now.AddHours(1));

        var customerId = Guid.NewGuid();
        var contactId = Guid.NewGuid();
        var oppId = Guid.NewGuid();

        lead.Convert(_actorId, _now.AddDays(1), customerId, contactId, oppId);

        Assert.Equal(LeadStatus.Converted, lead.Status);
        Assert.Equal(customerId, lead.ConvertedCustomerId);
        Assert.Equal(contactId, lead.ConvertedContactId);
        Assert.Equal(oppId, lead.ConvertedOpportunityId);

        var convEvent = Assert.IsType<LeadConvertedDomainEvent>(lead.DomainEvents.Last());
        Assert.Equal(customerId, convEvent.ConvertedAccountId);

        // Cannot re-convert
        var reConvertEx = Assert.Throws<DomainException>(() =>
            lead.Convert(_actorId, _now.AddDays(2), Guid.NewGuid(), null, null));
        Assert.Equal(ErrorCodes.LeadAlreadyConverted, reConvertEx.Code);
    }

    [Fact]
    public void Disqualify_RequiresReasonAndPreventsFurtherMutations()
    {
        var lead = Lead.Create(
            Guid.NewGuid(), _tenantId, _leadNumber, "Bob", "Vance", "Vance Refrigeration", null, null, null, null, _actorId, _now);

        var emptyReasonEx = Assert.Throws<DomainException>(() =>
            lead.Disqualify(_actorId, _now.AddDays(1), "   "));
        Assert.Equal(ErrorCodes.LeadDisqualificationReasonRequired, emptyReasonEx.Code);

        lead.Disqualify(_actorId, _now.AddDays(1), "Out of territory.");
        Assert.Equal(LeadStatus.Unqualified, lead.Status);
        Assert.Equal("Out of territory.", lead.DisqualificationReason);

        // Cannot contact after disqualification
        var terminalEx = Assert.Throws<DomainException>(() =>
            lead.MarkContacted(_actorId, _now.AddDays(2)));
        Assert.Equal(ErrorCodes.LeadInvalidStatusTransition, terminalEx.Code);
    }
}
