using CURE.Application.Shared;
using CURE.Domain.Shared;

namespace CURE.Application.Customers;

public interface ICustomerStore
{
    Task<CustomerPage> ListAsync(Guid tenantId, CustomerListQuery query, CancellationToken cancellationToken);
    Task<CustomerDetail?> GetAsync(Guid tenantId, Guid customerId, CancellationToken cancellationToken);
    Task<CustomerDetail?> FindDuplicateAsync(Guid tenantId, string? normalizedEmail, string? normalizedPhone, string normalizedName, CancellationToken cancellationToken);
    Task<CustomerDetail> CreateAsync(Guid tenantId, Guid actorId, string requestId, CreateCustomerCommand command, CancellationToken cancellationToken);
    Task<CustomerDetail?> UpdateAsync(Guid tenantId, Guid actorId, string requestId, Guid customerId, UpdateCustomerCommand command, CancellationToken cancellationToken);
    Task<bool> DeleteAsync(Guid tenantId, Guid actorId, string requestId, Guid customerId, CancellationToken cancellationToken);
}

public sealed class CustomerService(ICustomerStore store) : ICustomerService
{
    public Task<CustomerPage> ListAsync(Guid tenantId, CustomerListQuery query, CancellationToken cancellationToken)
        => store.ListAsync(tenantId, query with
        {
            Page = Math.Max(1, query.Page),
            PageSize = Math.Clamp(query.PageSize, 1, 100),
            Search = NormalizeFilter(query.Search),
            Type = NormalizeFilter(query.Type),
            Industry = NormalizeFilter(query.Industry)
        }, cancellationToken);

    private static string? NormalizeFilter(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    public async Task<CustomerDetail> GetAsync(Guid tenantId, Guid customerId, CancellationToken cancellationToken)
        => await store.GetAsync(tenantId, customerId, cancellationToken)
           ?? throw DomainException.NotFound("Customer", customerId);

    public async Task<CustomerDetail> CreateAsync(Guid tenantId, Guid actorId, string requestId, CreateCustomerCommand command, CancellationToken cancellationToken)
    {
        Validate(command);
        var duplicate = await store.FindDuplicateAsync(tenantId, Normalize(command.Email), NormalizePhone(command.Phone), Normalize(command.Name), cancellationToken);
        if (duplicate is not null)
            throw new DomainException(ErrorCodes.CustomerDuplicate, "A customer with this identity already exists.", new Dictionary<string, object?> { ["existingId"] = duplicate.Id });
        return await store.CreateAsync(tenantId, actorId, requestId, command, cancellationToken);
    }

    public async Task<CustomerDetail> UpdateAsync(Guid tenantId, Guid actorId, string requestId, Guid customerId, UpdateCustomerCommand command, CancellationToken cancellationToken)
    {
        Validate(command);
        _ = await GetAsync(tenantId, customerId, cancellationToken);
        return await store.UpdateAsync(tenantId, actorId, requestId, customerId, command, cancellationToken)
            ?? throw new DomainException(ErrorCodes.ConcurrentModification, "This record changed while you were editing it.");
    }

    public async Task DeleteAsync(Guid tenantId, Guid actorId, string requestId, Guid customerId, CancellationToken cancellationToken)
    {
        if (!await store.DeleteAsync(tenantId, actorId, requestId, customerId, cancellationToken))
            throw DomainException.NotFound("Customer", customerId);
    }

    public async Task<IReadOnlyList<DuplicateCandidate>> FindDuplicatesAsync(Guid tenantId, string? email, string? phone, string name, CancellationToken cancellationToken)
    {
        var duplicate = await store.FindDuplicateAsync(tenantId, Normalize(email), NormalizePhone(phone), Normalize(name), cancellationToken);
        return duplicate is null ? [] : [new DuplicateCandidate(duplicate.Id, duplicate.Name, "identity", 100)];
    }

    private static void Validate(CreateCustomerCommand command)
    {
        if (string.IsNullOrWhiteSpace(command.Name)) throw new DomainException(ErrorCodes.CustomerNameRequired, "A customer name is required.");
        if (command.Name.Length > 200) throw DomainException.Validation("Customer name cannot exceed 200 characters.");
        if (!string.IsNullOrWhiteSpace(command.Email)) EmailAddress.From(command.Email);
        if (!string.IsNullOrWhiteSpace(command.Phone)) PhoneNumber.From(command.Phone);
    }

    private static void Validate(UpdateCustomerCommand command) => Validate(new CreateCustomerCommand(command.Name, command.Type, command.Industry, command.Email, command.Phone, command.OwnerId));
    private static string? Normalize(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim().ToLowerInvariant();
    private static string? NormalizePhone(string? value) => string.IsNullOrWhiteSpace(value) ? null : new string(value.Where(char.IsAsciiDigit).ToArray());
}