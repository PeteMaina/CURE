namespace CURE.Application.Shared;

public sealed record RequestContext(Guid TenantId, Guid UserId, string RequestId);

public interface IRequestContextAccessor
{
    RequestContext Current { get; }
}