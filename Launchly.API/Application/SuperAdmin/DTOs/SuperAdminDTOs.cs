namespace Launchly.API.Application.SuperAdmin.DTOs;

// ─── Tenants ──────────────────────────────────────────────────────────────────

public record TenantListItemDto(
    Guid Id,
    string Name,
    string Subdomain,
    string StoreType,
    string PlanType,
    bool IsActive,
    int TotalUsers,
    int TotalOrders,
    decimal TotalRevenue,
    DateTime CreatedAt
);

public record TenantDetailDto(
    Guid Id,
    string Name,
    string Subdomain,
    string StoreType,
    string PlanType,
    bool IsActive,
    string? LogoUrl,
    string? StoreName,
    int TotalUsers,
    int TotalProducts,
    int TotalOrders,
    decimal TotalRevenue,
    DateTime CreatedAt
);

public record UpdateTenantStatusRequest(
    bool IsActive
);

public record TenantsQuery(
    int Page = 1,
    int PageSize = 20,
    string? Search = null,
    bool? IsActive = null
);

// ─── Platform Analytics ───────────────────────────────────────────────────────

public record PlatformStatsDto(
    int TotalTenants,
    int ActiveTenants,
    int TotalUsers,
    int TotalOrders,
    decimal TotalRevenue,
    int NewTenantsThisMonth,
    IReadOnlyList<PlanBreakdownDto> PlanBreakdown,
    IReadOnlyList<StoreTypeBreakdownDto> StoreTypeBreakdown
);

public record PlanBreakdownDto(
    string Plan,
    int Count
);

public record StoreTypeBreakdownDto(
    string StoreType,
    int Count
);

// ─── Platform Audit Log ───────────────────────────────────────────────────────

public record SuperAuditLogDto(
    Guid Id,
    string? TenantName,
    string UserEmail,
    string Action,
    string? EntityType,
    Guid? EntityId,
    string? Details,
    string? IpAddress,
    DateTime CreatedAt
);

public record SuperAuditLogListDto(
    IReadOnlyList<SuperAuditLogDto> Items,
    int TotalCount,
    int Page,
    int PageSize,
    int TotalPages
);
