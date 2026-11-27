namespace CURE.Domain.Pipeline;

/// <summary>
/// Lifecycle status of an inbound or outbound commercial lead.
/// </summary>
public enum LeadStatus
{
    New = 1,
    Contacted = 2,
    Engaged = 3,
    Qualified = 4,
    Unqualified = 5,
    Converted = 6,
}

public static class LeadStatusExtensions
{
    public static bool IsTerminal(this LeadStatus status)
        => status is LeadStatus.Unqualified or LeadStatus.Converted;

    public static bool IsActive(this LeadStatus status)
        => !status.IsTerminal();

    public static bool CanConvert(this LeadStatus status)
        => status == LeadStatus.Qualified;

    public static string DisplayName(this LeadStatus status) => status switch
    {
        LeadStatus.New => "New",
        LeadStatus.Contacted => "Contacted",
        LeadStatus.Engaged => "Engaged",
        LeadStatus.Qualified => "Qualified",
        LeadStatus.Unqualified => "Unqualified",
        LeadStatus.Converted => "Converted",
        _ => status.ToString(),
    };
}
