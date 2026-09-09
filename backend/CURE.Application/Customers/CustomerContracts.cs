namespace CURE.Application.Customers;

public sealed record CustomerListQuery(string? Search, int Page, int PageSize);

public sealed record CustomerListItem(
    Guid Id,
    string Name,
    string Type,
    string? Industry,
    Guid? OwnerId,
    string? OwnerName,
    DateTimeOffset UpdatedAt,
    int Version);

public sealed record CustomerDetail(
    Guid Id,
    string Name,
    string Type,
    string? Industry,
    string? Email,
    string? Phone,
    Guid? OwnerId,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    int Version);

public sealed record CustomerPage(IReadOnlyList<CustomerListItem> Data, int Page, int PageSize, int Total);

public sealed record CreateCustomerCommand(
    string Name,
    string Type,
    string? Industry,
    string? Email,
    string? Phone,
    Guid? OwnerId);

public sealed record UpdateCustomerCommand(
    string Name,
    string Type,
    string? Industry,
    string? Email,
    string? Phone,
    Guid? OwnerId,
    int ExpectedVersion);

public sealed record DuplicateCandidate(Guid Id, string Name, string MatchField, int Similarity);

public interface ICustomerService
{
    Task<CustomerPage> ListAsync(Guid tenantId, CustomerListQuery query, CancellationToken cancellationToken);
    Task<CustomerDetail> GetAsync(Guid tenantId, Guid customerId, CancellationToken cancellationToken);
    Task<CustomerDetail> CreateAsync(Guid tenantId, Guid actorId, string requestId, CreateCustomerCommand command, CancellationToken cancellationToken);
    Task<CustomerDetail> UpdateAsync(Guid tenantId, Guid actorId, string requestId, Guid customerId, UpdateCustomerCommand command, CancellationToken cancellationToken);
    Task DeleteAsync(Guid tenantId, Guid actorId, string requestId, Guid customerId, CancellationToken cancellationToken);
    Task<IReadOnlyList<DuplicateCandidate>> FindDuplicatesAsync(Guid tenantId, string? email, string? phone, string name, CancellationToken cancellationToken);
}