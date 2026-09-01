namespace CURE.Domain.Shared;


/// Public entity identifier generation.

public static class CureId
{
    public static Guid New() => Guid.CreateVersion7();

    public static Guid NewAt(DateTimeOffset timestamp) => Guid.CreateVersion7(timestamp);

    
    /// Parses an identifier supplied by a client. Returns false for anything
    /// malformed so callers can answer 404 rather than 500.
    /// </summary>
    public static bool TryParse(string? value, out Guid id)
        => Guid.TryParse(value, out id) && id != Guid.Empty;
}
