namespace CURE.Domain.Shared;

/// <summary>
/// Sensitivity of a record (CURE.md 33).
///
/// Classification is not decoration: it feeds visibility, export eligibility,
/// audit depth, sharing and retention. A RESTRICTED customer is not merely
/// labelled — the export path and the AI data boundary both consult this value.
/// </summary>
public enum DataClassification
{
    /// <summary>Safe to surface anywhere in the tenant, including public-facing summaries.</summary>
    Public = 0,

    /// <summary>Default for ordinary CRM records. Visible to authorised tenant users.</summary>
    Internal = 1,

    /// <summary>Commercially sensitive. Export requires elevated permission and is always audited.</summary>
    Confidential = 2,

    /// <summary>
    /// Highest sensitivity. Access is logged as a security event, export requires
    /// approval, and the record is excluded from external AI processing.
    /// </summary>
    Restricted = 3,
}

public static class DataClassificationRules
{
    /// Whether merely reading the record is itself an auditable event
    /// (CUSTOMER_VIEWED_SENSITIVE ).
    
    public static bool ReadIsAuditable(this DataClassification classification)
        => classification >= DataClassification.Confidential;

    /// <summary>Whether exporting requires an approval decision (CURE.md 78).</summary>
    public static bool ExportRequiresApproval(this DataClassification classification)
        => classification >= DataClassification.Restricted;

    /// Whether the record may leave the system boundary for external model
    /// processing . Restricted data never does.
    public static bool MayBeSentToExternalModel(this DataClassification classification)
        => classification <= DataClassification.Confidential;
}
