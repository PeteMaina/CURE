namespace CURE.Domain.Pipeline;

using CURE.Domain.Shared;

/// <summary>
/// Invariant stage gate validator enforcing the opportunity state machine.
/// Prevents illegal skips, unauthorized closures, or winning unbacked deals.
/// </summary>
public static class OpportunityStageGate
{
    private static readonly Dictionary<OpportunityStage, HashSet<OpportunityStage>> AllowedTransitions = new()
    {
        [OpportunityStage.Qualification] = new() { OpportunityStage.Discovery, OpportunityStage.ClosedLost },
        [OpportunityStage.Discovery] = new() { OpportunityStage.Qualification, OpportunityStage.Proposal, OpportunityStage.ClosedLost },
        [OpportunityStage.Proposal] = new() { OpportunityStage.Discovery, OpportunityStage.Negotiation, OpportunityStage.ClosedWon, OpportunityStage.ClosedLost },
        [OpportunityStage.Negotiation] = new() { OpportunityStage.Proposal, OpportunityStage.ClosedWon, OpportunityStage.ClosedLost },
        [OpportunityStage.ClosedWon] = new() { OpportunityStage.Negotiation, OpportunityStage.Proposal },
        [OpportunityStage.ClosedLost] = new() { OpportunityStage.Qualification, OpportunityStage.Discovery },
    };

    public static bool CanTransition(OpportunityStage from, OpportunityStage to)
        => AllowedTransitions.TryGetValue(from, out var targets) && targets.Contains(to);

    public static void ValidateTransition(
        OpportunityStage currentStage,
        OpportunityStage targetStage,
        Money amount,
        Guid? customerId,
        string? closeReason,
        bool isOverride = false)
    {
        if (currentStage == targetStage)
        {
            throw new DomainException(
                ErrorCodes.OpportunityInvalidStageTransition,
                $"Opportunity is already in stage '{currentStage.DisplayName()}'.");
        }

        if (currentStage.IsClosed() && !targetStage.IsOpen())
        {
            throw new DomainException(
                ErrorCodes.OpportunityAlreadyClosed,
                "A closed opportunity cannot be moved directly to another closed state. Reopen first.");
        }

        if (!isOverride && !CanTransition(currentStage, targetStage))
        {
            throw new DomainException(
                ErrorCodes.OpportunityInvalidStageTransition,
                $"Illegal stage transition from '{currentStage.DisplayName()}' to '{targetStage.DisplayName()}'.",
                new Dictionary<string, object?>
                {
                    ["currentStage"] = currentStage.ToString(),
                    ["targetStage"] = targetStage.ToString(),
                });
        }

        if (targetStage.IsWon())
        {
            if (amount.Amount <= 0)
            {
                throw new DomainException(
                    ErrorCodes.OpportunityAmountRequired,
                    "An opportunity cannot be won without a positive confirmed deal amount.");
            }

            if (!customerId.HasValue || customerId.Value == Guid.Empty)
            {
                throw DomainException.Validation(
                    "An opportunity cannot be marked Closed-Won without an assigned Customer account.");
            }

            if (string.IsNullOrWhiteSpace(closeReason))
            {
                throw new DomainException(
                    ErrorCodes.OpportunityCloseReasonRequired,
                    "A valid win reason is mandatory when closing an opportunity as Won.");
            }
        }

        if (targetStage.IsLost() && string.IsNullOrWhiteSpace(closeReason))
        {
            throw new DomainException(
                ErrorCodes.OpportunityCloseReasonRequired,
                "A reason must be recorded when closing an opportunity as Lost.");
        }
    }
}
