using CURE.Domain.Shared;

namespace CURE.Domain.Tenancy;

public enum TenantStatus
{
    Active = 0,
    Suspended = 1,
    Closed = 2,
}

/// A customer organisation using CURE.

public sealed class Tenant
{
    private Tenant(
        Guid id,
        string name,
        string slug,
        TenantStatus status,
        CurrencyCode defaultCurrency,
        string defaultTimeZoneId,
        string defaultDiallingCode,
        DateTimeOffset createdAt,
        DateTimeOffset updatedAt,
        int version)
    {
        Id = id;
        Name = name;
        Slug = slug;
        Status = status;
        DefaultCurrency = defaultCurrency;
        DefaultTimeZoneId = defaultTimeZoneId;
        DefaultDiallingCode = defaultDiallingCode;
        CreatedAt = createdAt;
        UpdatedAt = updatedAt;
        Version = version;
    }

    public Guid Id { get; }

    public string Name { get; private set; }

    /// <summary>Stable URL-safe key. Immutable once issued — it appears in support tooling.</summary>
    public string Slug { get; }

    public TenantStatus Status { get; private set; }

    /// <summary>
    /// Reporting currency. Individual records still carry their own currency;
    /// this is the default for new records and the roll-up currency for
    /// tenant-wide financial summaries 
    public CurrencyCode DefaultCurrency { get; private set; }

    public string DefaultTimeZoneId { get; private set; }

    /// <summary>
    /// International dialling prefix used to promote locally formatted phone
    /// numbers, and to phrase phone validation messages (CURE.md 53).
    /// </summary>
    public string DefaultDiallingCode { get; private set; }

    public DateTimeOffset CreatedAt { get; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public int Version { get; private set; }

    public bool IsOperational => Status == TenantStatus.Active;

    public static Tenant Create(
        Guid id,
        string name,
        string slug,
        CurrencyCode defaultCurrency,
        string defaultTimeZoneId,
        string defaultDiallingCode,
        DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw DomainException.Validation("A tenant name is required.");
        }

        if (string.IsNullOrWhiteSpace(slug))
        {
            throw DomainException.Validation("A tenant slug is required.");
        }

        return new Tenant(
            id,
            name.Trim(),
            slug.Trim().ToLowerInvariant(),
            TenantStatus.Active,
            defaultCurrency,
            defaultTimeZoneId,
            defaultDiallingCode,
            now,
            now,
            1);
    }

    public static Tenant Rehydrate(
        Guid id,
        string name,
        string slug,
        TenantStatus status,
        CurrencyCode defaultCurrency,
        string defaultTimeZoneId,
        string defaultDiallingCode,
        DateTimeOffset createdAt,
        DateTimeOffset updatedAt,
        int version)
        => new(id, name, slug, status, defaultCurrency, defaultTimeZoneId,
               defaultDiallingCode, createdAt, updatedAt, version);

    public void Rename(string name, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw DomainException.Validation("A tenant name is required.");
        }

        Name = name.Trim();
        UpdatedAt = now;
    }

    public void Suspend(DateTimeOffset now)
    {
        Status = TenantStatus.Suspended;
        UpdatedAt = now;
    }

    public void Reactivate(DateTimeOffset now)
    {
        Status = TenantStatus.Active;
        UpdatedAt = now;
    }

    public void MarkPersisted(int persistedVersion) => Version = persistedVersion;
}
