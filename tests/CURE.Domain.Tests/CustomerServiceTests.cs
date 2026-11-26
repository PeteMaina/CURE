using CURE.Application.Customers;
using Xunit;

namespace CURE.Domain.Tests;

public sealed class CustomerServiceTests
{
    [Fact]
    public async Task ListNormalizesPagingAndFilterValuesBeforePersistence()
    {
        var store = new FakeCustomerStore();
        var service = new CustomerService(store);

        await service.ListAsync(
            Guid.NewGuid(),
            new CustomerListQuery("  Acme  ", 0, 500, " Business ", " "),
            CancellationToken.None);

        Assert.NotNull(store.ListQuery);
        Assert.Equal("Acme", store.ListQuery.Search);
        Assert.Equal(1, store.ListQuery.Page);
        Assert.Equal(100, store.ListQuery.PageSize);
        Assert.Equal("Business", store.ListQuery.Type);
        Assert.Null(store.ListQuery.Industry);
    }

    [Fact]
    public async Task Create_rejects_duplicate_identity_before_persistence()
    {
        var existing = new CustomerDetail(Guid.NewGuid(), "Acme", "Business", null, null, null, null, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, 1);
        var store = new FakeCustomerStore { Duplicate = existing };
        var service = new CustomerService(store);

        var exception = await Assert.ThrowsAsync<CURE.Domain.Shared.DomainException>(() => service.CreateAsync(Guid.NewGuid(), Guid.NewGuid(), "request-1", new CreateCustomerCommand("Acme", "Business", null, null, null, null), CancellationToken.None));

        Assert.Equal(CURE.Domain.Shared.ErrorCodes.CustomerDuplicate, exception.Code);
        Assert.Equal(0, store.CreateCalls);
    }

    [Fact]
    public async Task Create_validates_email_before_persistence()
    {
        var store = new FakeCustomerStore();
        var service = new CustomerService(store);

        var exception = await Assert.ThrowsAsync<CURE.Domain.Shared.DomainException>(() => service.CreateAsync(Guid.NewGuid(), Guid.NewGuid(), "request-2", new CreateCustomerCommand("Acme", "Business", null, "not-an-email", null, null), CancellationToken.None));

        Assert.Equal(CURE.Domain.Shared.ErrorCodes.InvalidEmail, exception.Code);
        Assert.Equal(0, store.CreateCalls);
    }

    private sealed class FakeCustomerStore : ICustomerStore
    {
        public CustomerDetail? Duplicate { get; init; }
        public int CreateCalls { get; private set; }
        public CustomerListQuery? ListQuery { get; private set; }
        public Task<CustomerPage> ListAsync(Guid tenantId, CustomerListQuery query, CancellationToken cancellationToken)
        {
            ListQuery = query;
            return Task.FromResult(new CustomerPage([], query.Page, query.PageSize, 0));
        }
        public Task<CustomerDetail?> GetAsync(Guid tenantId, Guid customerId, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task<CustomerDetail?> FindDuplicateAsync(Guid tenantId, string? normalizedEmail, string? normalizedPhone, string normalizedName, CancellationToken cancellationToken) => Task.FromResult(Duplicate);
        public Task<CustomerDetail> CreateAsync(Guid tenantId, Guid actorId, string requestId, CreateCustomerCommand command, CancellationToken cancellationToken) { CreateCalls++; return Task.FromResult(new CustomerDetail(Guid.NewGuid(), command.Name, command.Type, command.Industry, command.Email, command.Phone, command.OwnerId, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, 1)); }
        public Task<CustomerDetail?> UpdateAsync(Guid tenantId, Guid actorId, string requestId, Guid customerId, UpdateCustomerCommand command, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task<bool> DeleteAsync(Guid tenantId, Guid actorId, string requestId, Guid customerId, CancellationToken cancellationToken) => Task.FromResult(false);
    }
}
