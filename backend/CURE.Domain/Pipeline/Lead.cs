namespace CURE.Domain.Pipeline;

using CURE.Domain.Pipeline.Events;
using CURE.Domain.Shared;

/// <summary>
/// Lead aggregate root tracking pre-qualification prospects up to conversion.
/// </summary>
public sealed class Lead : AggregateRoot
{
    private Lead(
        Guid id,
        Guid tenantId,
        EntityNumber leadNumber,
        string firstName,
        string lastName,
        string companyName,
        EmailAddress? email,
        PhoneNumber? phone,
        LeadStatus status,
        string? leadSource,
        string? disqualificationReason,
        Guid? convertedCustomerId,
        Guid? convertedContactId,
        Guid? convertedOpportunityId,
        DateTimeOffset? convertedAt,
        Guid? ownerId,
        DateTimeOffset createdAt,
        Guid createdBy,
        DateTimeOffset updatedAt,
        Guid updatedBy,
        int version)
        : base(id, tenantId, createdAt, createdBy, updatedAt, updatedBy, version)
    {
        LeadNumber = leadNumber;
        FirstName = firstName;
        LastName = lastName;
        CompanyName = companyName;
        Email = email;
        Phone = phone;
        Status = status;
        LeadSource = leadSource;
        DisqualificationReason = disqualificationReason;
        ConvertedCustomerId = convertedCustomerId;
        ConvertedContactId = convertedContactId;
        ConvertedOpportunityId = convertedOpportunityId;
        ConvertedAt = convertedAt;
        OwnerId = ownerId;
    }

    public EntityNumber LeadNumber { get; }

    public string FirstName { get; private set; }

    public string LastName { get; private set; }

    public string FullName => string.IsNullOrWhiteSpace(FirstName) ? LastName : $"{FirstName} {LastName}".Trim();

    public string CompanyName { get; private set; }

    public EmailAddress? Email { get; private set; }

    public PhoneNumber? Phone { get; private set; }

    public LeadStatus Status { get; private set; }

    public string? LeadSource { get; private set; }

    public string? DisqualificationReason { get; private set; }

    public Guid? ConvertedCustomerId { get; private set; }

    public Guid? ConvertedContactId { get; private set; }

    public Guid? ConvertedOpportunityId { get; private set; }

    public DateTimeOffset? ConvertedAt { get; private set; }

    public Guid? OwnerId { get; private set; }

    public static Lead Create(
        Guid id,
        Guid tenantId,
        EntityNumber leadNumber,
        string firstName,
        string lastName,
        string companyName,
        EmailAddress? email,
        PhoneNumber? phone,
        string? leadSource,
        Guid? ownerId,
        Guid actorId,
        DateTimeOffset at)
    {
        if (string.IsNullOrWhiteSpace(lastName))
        {
            throw DomainException.Validation("Lead last name is required.");
        }

        if (string.IsNullOrWhiteSpace(companyName))
        {
            throw DomainException.Validation("Lead company name is required.");
        }

        var lead = new Lead(
            id: id,
            tenantId: tenantId,
            leadNumber: leadNumber,
            firstName: firstName?.Trim() ?? string.Empty,
            lastName: lastName.Trim(),
            companyName: companyName.Trim(),
            email: email,
            phone: phone,
            status: LeadStatus.New,
            leadSource: leadSource?.Trim(),
            disqualificationReason: null,
            convertedCustomerId: null,
            convertedContactId: null,
            convertedOpportunityId: null,
            convertedAt: null,
            ownerId: ownerId,
            createdAt: at,
            createdBy: actorId,
            updatedAt: at,
            updatedBy: actorId,
            version: 1);

        lead.Raise(new LeadCreatedDomainEvent(
            tenantId,
            at,
            id,
            leadNumber.Value,
            lead.FullName,
            companyName.Trim(),
            leadSource?.Trim(),
            actorId));

        return lead;
    }

    public void MarkContacted(Guid actorId, DateTimeOffset at)
    {
        AssertNotTerminal();
        TransitionTo(LeadStatus.Contacted, actorId, at);
    }

    public void MarkEngaged(Guid actorId, DateTimeOffset at)
    {
        AssertNotTerminal();
        TransitionTo(LeadStatus.Engaged, actorId, at);
    }

    public void Qualify(Guid actorId, DateTimeOffset at)
    {
        AssertNotTerminal();
        TransitionTo(LeadStatus.Qualified, actorId, at);
    }

    public void Disqualify(Guid actorId, DateTimeOffset at, string reason)
    {
        AssertNotTerminal();

        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new DomainException(
                ErrorCodes.LeadDisqualificationReasonRequired,
                "A reason is required to disqualify a lead.");
        }

        var old = Status;
        Status = LeadStatus.Unqualified;
        DisqualificationReason = reason.Trim();
        Touch(actorId, at);

        Raise(new LeadDisqualifiedDomainEvent(TenantId, at, Id, LeadNumber.Value, reason.Trim(), actorId));
    }

    public void Convert(
        Guid actorId,
        DateTimeOffset at,
        Guid customerId,
        Guid? contactId,
        Guid? opportunityId)
    {
        if (Status == LeadStatus.Converted)
        {
            throw new DomainException(
                ErrorCodes.LeadAlreadyConverted,
                "Lead has already been converted.");
        }

        if (Status != LeadStatus.Qualified)
        {
            throw new DomainException(
                ErrorCodes.LeadNotQualified,
                $"Only qualified leads can be converted. Current status is '{Status.DisplayName()}'.");
        }

        if (customerId == Guid.Empty)
        {
            throw new DomainException(
                ErrorCodes.LeadConversionRequirementsUnmet,
                "Conversion requires an existing or newly created Customer/Account ID.");
        }

        Status = LeadStatus.Converted;
        ConvertedCustomerId = customerId;
        ConvertedContactId = contactId;
        ConvertedOpportunityId = opportunityId;
        ConvertedAt = at;
        Touch(actorId, at);

        Raise(new LeadConvertedDomainEvent(
            TenantId,
            at,
            Id,
            LeadNumber.Value,
            customerId,
            contactId,
            opportunityId,
            actorId));
    }

    private void TransitionTo(LeadStatus newStatus, Guid actorId, DateTimeOffset at)
    {
        if (Status == newStatus) return;

        var oldStatus = Status;
        Status = newStatus;
        Touch(actorId, at);

        Raise(new LeadStatusChangedDomainEvent(TenantId, at, Id, LeadNumber.Value, oldStatus, newStatus, actorId));
    }

    private void AssertNotTerminal()
    {
        if (Status.IsTerminal())
        {
            throw new DomainException(
                ErrorCodes.LeadInvalidStatusTransition,
                $"Cannot transition lead from terminal status '{Status.DisplayName()}'.");
        }
    }
}
