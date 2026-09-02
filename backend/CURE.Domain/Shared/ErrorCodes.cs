namespace CURE.Domain.Shared;

/// <summary>
/// Stable, machine-readable business error codes 
///
/// The frontend branches on these codes and never on message text. These are to be edited later though

public static class ErrorCodes
{
    // ---- Generic ----------------------------------------------------------
    public const string ValidationFailed = "VALIDATION_FAILED";
    public const string NotFound = "NOT_FOUND";
    public const string InsufficientPermission = "INSUFFICIENT_PERMISSION";
    public const string ConcurrentModification = "CONCURRENT_MODIFICATION";
    public const string IdempotencyKeyReused = "IDEMPOTENCY_KEY_REUSED";
    public const string RateLimited = "RATE_LIMITED";
    public const string TenantContextMissing = "TENANT_CONTEXT_MISSING";
    public const string CrossTenantAccessDenied = "CROSS_TENANT_ACCESS_DENIED";
    public const string ReauthenticationRequired = "REAUTHENTICATION_REQUIRED";
    public const string ApprovalRequired = "APPROVAL_REQUIRED";

    // ---- Value objects ----------------------------------------------------
    public const string InvalidEmail = "INVALID_EMAIL";
    public const string InvalidPhone = "INVALID_PHONE";
    public const string InvalidCurrency = "INVALID_CURRENCY";
    public const string CurrencyMismatch = "CURRENCY_MISMATCH";
    public const string InvalidPercentage = "INVALID_PERCENTAGE";
    public const string InvalidDateRange = "INVALID_DATE_RANGE";
    public const string InvalidEntityNumber = "INVALID_ENTITY_NUMBER";
    public const string NegativeAmountNotAllowed = "NEGATIVE_AMOUNT_NOT_ALLOWED";

    // ---- Identity / access ------------------------------------------------
    public const string InvalidCredentials = "INVALID_CREDENTIALS";
    public const string AccountLocked = "ACCOUNT_LOCKED";
    public const string AccountInactive = "ACCOUNT_INACTIVE";
    public const string SessionExpired = "SESSION_EXPIRED";
    public const string SessionRevoked = "SESSION_REVOKED";
    public const string MfaRequired = "MFA_REQUIRED";
    public const string MfaInvalidCode = "MFA_INVALID_CODE";

    // ---- Customers --------------------------------------------------------
    public const string CustomerDuplicate = "CUSTOMER_DUPLICATE";
    public const string CustomerArchived = "CUSTOMER_ARCHIVED";
    public const string CustomerMergeSelfReference = "CUSTOMER_MERGE_SELF_REFERENCE";
    public const string CustomerMergeArchivedSurvivor = "CUSTOMER_MERGE_ARCHIVED_SURVIVOR";
    public const string CustomerNameRequired = "CUSTOMER_NAME_REQUIRED";

    // ---- Leads ------------------------------------------------------------
    public const string LeadInvalidStatusTransition = "LEAD_INVALID_STATUS_TRANSITION";
    public const string LeadNotQualified = "LEAD_NOT_QUALIFIED";
    public const string LeadAlreadyConverted = "LEAD_ALREADY_CONVERTED";
    public const string LeadConversionRequirementsUnmet = "LEAD_CONVERSION_REQUIREMENTS_UNMET";
    public const string LeadDisqualificationReasonRequired = "LEAD_DISQUALIFICATION_REASON_REQUIRED";

    // ---- Opportunities ----------------------------------------------------
    public const string OpportunityInvalidStageTransition = "OPPORTUNITY_INVALID_STAGE_TRANSITION";
    public const string OpportunityAlreadyClosed = "OPPORTUNITY_ALREADY_CLOSED";
    public const string OpportunityCloseReasonRequired = "OPPORTUNITY_CLOSE_REASON_REQUIRED";
    public const string OpportunityAmountRequired = "OPPORTUNITY_AMOUNT_REQUIRED";
    public const string OpportunityStageNotInPipeline = "OPPORTUNITY_STAGE_NOT_IN_PIPELINE";
    public const string DiscountExceedsAuthority = "DISCOUNT_EXCEEDS_AUTHORITY";

    // ---- Cases / SLA ------------------------------------------------------
    public const string CaseInvalidStatusTransition = "CASE_INVALID_STATUS_TRANSITION";
    public const string CaseResolutionMetadataRequired = "CASE_RESOLUTION_METADATA_REQUIRED";
    public const string CaseAlreadyClosed = "CASE_ALREADY_CLOSED";
    public const string SlaPolicyNotFound = "SLA_POLICY_NOT_FOUND";
    public const string BusinessCalendarNotFound = "BUSINESS_CALENDAR_NOT_FOUND";
    public const string BusinessCalendarHasNoWorkingTime = "BUSINESS_CALENDAR_HAS_NO_WORKING_TIME";

    // ---- Activities / tasks -----------------------------------------------
    public const string ActivityInvalidStatusTransition = "ACTIVITY_INVALID_STATUS_TRANSITION";
    public const string ActivityOutcomeRequired = "ACTIVITY_OUTCOME_REQUIRED";
    public const string TaskInvalidStatusTransition = "TASK_INVALID_STATUS_TRANSITION";
    public const string TaskCompletionReasonRequired = "TASK_COMPLETION_REASON_REQUIRED";

    // ---- Commitments ------------------------------------------------------
    public const string CommitmentAlreadySettled = "COMMITMENT_ALREADY_SETTLED";
    public const string CommitmentDueDateRequired = "COMMITMENT_DUE_DATE_REQUIRED";

    // ---- Intelligence -----------------------------------------------------
    public const string SignalAlreadySettled = "SIGNAL_ALREADY_SETTLED";
    public const string OverrideReasonRequired = "OVERRIDE_REASON_REQUIRED";
    public const string HealthWeightsInvalid = "HEALTH_WEIGHTS_INVALID";

    // ---- Workflow ---------------------------------------------------------
    public const string WorkflowDefinitionInvalid = "WORKFLOW_DEFINITION_INVALID";
    public const string WorkflowStepLimitExceeded = "WORKFLOW_STEP_LIMIT_EXCEEDED";
    public const string WorkflowRecursionDetected = "WORKFLOW_RECURSION_DETECTED";
    public const string WorkflowRetryLimitExceeded = "WORKFLOW_RETRY_LIMIT_EXCEEDED";
    public const string WorkflowExecutionNotCancellable = "WORKFLOW_EXECUTION_NOT_CANCELLABLE";

    // ---- Approvals --------------------------------------------------------
    public const string ApprovalAlreadyDecided = "APPROVAL_ALREADY_DECIDED";
    public const string ApprovalSelfApprovalForbidden = "APPROVAL_SELF_APPROVAL_FORBIDDEN";

    // ---- Export / import --------------------------------------------------
    public const string ExportVolumeLimitExceeded = "EXPORT_VOLUME_LIMIT_EXCEEDED";
    public const string ImportFileInvalid = "IMPORT_FILE_INVALID";
    public const string ImportColumnMappingInvalid = "IMPORT_COLUMN_MAPPING_INVALID";
}
