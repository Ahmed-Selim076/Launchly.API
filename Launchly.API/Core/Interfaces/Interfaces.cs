using Launchly.API.Core.Enums;

namespace Launchly.API.Core.Interfaces;

public interface ITenantContext
{
    Guid? TenantId { get; }
    string? Subdomain { get; }
    void Set(Guid tenantId, string subdomain);
}

public interface ICurrentUser
{
    Guid Id { get; }
    string Email { get; }
    UserRole Role { get; }
    Guid? TenantId { get; }
    bool IsAuthenticated { get; }
}