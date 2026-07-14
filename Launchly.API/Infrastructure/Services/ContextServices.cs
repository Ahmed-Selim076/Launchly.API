using System.Security.Claims;
using Launchly.API.Core.Enums;
using Launchly.API.Core.Interfaces;

namespace Launchly.API.Infrastructure.Services;

public class TenantContext : ITenantContext
{
    public Guid? TenantId { get; private set; }
    public string? Subdomain { get; private set; }

    public void Set(Guid tenantId, string subdomain)
    {
        TenantId = tenantId;
        Subdomain = subdomain;
    }
}

public class CurrentUser : ICurrentUser
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CurrentUser(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    private ClaimsPrincipal? Principal =>
        _httpContextAccessor.HttpContext?.User;

    public bool IsAuthenticated =>
        Principal?.Identity?.IsAuthenticated ?? false;

    public Guid Id =>
        Guid.Parse(Principal?.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? Guid.Empty.ToString());

    public string Email =>
        Principal?.FindFirstValue(ClaimTypes.Email) ?? string.Empty;

    public UserRole Role =>
        Enum.Parse<UserRole>(
            Principal?.FindFirstValue(ClaimTypes.Role)
            ?? nameof(UserRole.Customer));

    public Guid? TenantId
    {
        get
        {
            var claim = Principal?.FindFirstValue("tenantId");
            return claim is null ? null : Guid.Parse(claim);
        }
    }
}