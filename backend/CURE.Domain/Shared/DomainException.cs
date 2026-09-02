namespace CURE.Domain.Shared;

/// <summary>
/// A rejected business operation.
///
/// Carries a stable ErrorCodes so the API
/// can map it to an HTTP status and the frontend can branch deterministically

public class DomainException : Exception
{
    public string Code { get; }

    public IReadOnlyDictionary<string, object?> Details { get; }

    public DomainException(string code, string message, IReadOnlyDictionary<string, object?>? details = null)
        : base(message)
    {
        Code = code;
        Details = details ?? EmptyDetails;
    }

    private static readonly IReadOnlyDictionary<string, object?> EmptyDetails =
        new Dictionary<string, object?>();

    public static DomainException Validation(string message, IReadOnlyDictionary<string, object?>? details = null)
        => new(ErrorCodes.ValidationFailed, message, details);

    public static DomainException NotFound(string entity, object id)
        => new(ErrorCodes.NotFound, $"{entity} was not found.", new Dictionary<string, object?>
        {
            ["entity"] = entity,
            ["id"] = id.ToString(),
        });
}


public sealed class ConcurrencyException : DomainException
{
    public int ExpectedVersion { get; }

    public int? ActualVersion { get; }

    public ConcurrencyException(string entity, int expectedVersion, int? actualVersion)
        : base(
            ErrorCodes.ConcurrentModification,
            "This record changed while you were editing it.",
            new Dictionary<string, object?>
            {
                ["entity"] = entity,
                ["expectedVersion"] = expectedVersion,
                ["actualVersion"] = actualVersion,
            })
    {
        ExpectedVersion = expectedVersion;
        ActualVersion = actualVersion;
    }
}

/// <summary>
/// Raised when the caller is authenticated but not permitted.

public sealed class PermissionDeniedException : DomainException
{
    public PermissionDeniedException(string? requiredPermission = null)
        : base(
            ErrorCodes.InsufficientPermission,
            "You do not have permission to perform this action.",
            requiredPermission is null
                ? null
                : new Dictionary<string, object?> { ["requiredPermission"] = requiredPermission })
    {
    }
}
