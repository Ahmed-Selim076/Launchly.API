using Microsoft.EntityFrameworkCore;
using Launchly.API.Application.SuperAdmin.DTOs;
using Launchly.API.Common;
using Launchly.API.Core.Enums;
using Launchly.API.Infrastructure.Data;
using Launchly.API.Infrastructure.Services;

namespace Launchly.API.Application.SuperAdmin;

public class SuperAdminService
{
    private readonly AppDbContext _db;
    private readonly AuditLogService _auditLog;

    public SuperAdminService(AppDbContext db, AuditLogService auditLog)
    {
        _db = db;
        _auditLog = auditLog;
    }

    // ─── List Tenants ─────────────────────────────────────────────────────────

    public async Task<Result<PagedResult<TenantListItemDto>>> GetTenantsAsync(TenantsQuery query)
    {
        // SuperAdmin bypasses tenant isolation — IgnoreQueryFilters not needed
        // because Tenants table has no Global Query Filter (it's not a TenantEntity)
        var q = _db.Tenants
            .AsNoTracking()
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim().ToLower();
            q = q.Where(t => t.Name.ToLower().Contains(term) ||
                              t.Subdomain.ToLower().Contains(term));
        }

        if (query.IsActive.HasValue)
            q = q.Where(t => t.IsActive == query.IsActive.Value);

        var totalCount = await q.CountAsync();

        var tenants = await q
            .OrderByDescending(t => t.CreatedAt)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(t => new
            {
                t.Id,
                t.Name,
                t.Subdomain,
                t.StoreType,
                t.PlanType,
                t.IsActive,
                t.CreatedAt,
                TotalUsers = t.Users.Count(u => u.Role == UserRole.Customer),
                TotalOrders = t.Orders.Count(o => o.DeletedAt == null),
                TotalRevenue = t.Orders
                    .Where(o => o.DeletedAt == null && o.Status != OrderStatus.Cancelled)
                    .Sum(o => (decimal?)o.TotalAmount) ?? 0
            })
            .ToListAsync();

        var items = tenants.Select(t => new TenantListItemDto(
            t.Id,
            t.Name,
            t.Subdomain,
            t.StoreType.ToString(),
            t.PlanType.ToString(),
            t.IsActive,
            t.TotalUsers,
            t.TotalOrders,
            t.TotalRevenue,
            t.CreatedAt
        )).ToList();

        return Result<PagedResult<TenantListItemDto>>.Success(new PagedResult<TenantListItemDto>
        {
            Items = items,
            TotalCount = totalCount,
            Page = query.Page,
            PageSize = query.PageSize
        });
    }

    // ─── Tenant Detail ────────────────────────────────────────────────────────

    public async Task<Result<TenantDetailDto>> GetTenantAsync(Guid id)
    {
        var tenant = await _db.Tenants
            .AsNoTracking()
            .Include(t => t.Settings)
            .Where(t => t.Id == id)
            .Select(t => new
            {
                t.Id,
                t.Name,
                t.Subdomain,
                t.StoreType,
                t.PlanType,
                t.IsActive,
                t.CreatedAt,
                t.Settings,
                TotalUsers = t.Users.Count(u => u.Role == UserRole.Customer),
                TotalProducts = t.Products.Count(p => p.DeletedAt == null),
                TotalOrders = t.Orders.Count(o => o.DeletedAt == null),
                TotalRevenue = t.Orders
                    .Where(o => o.DeletedAt == null && o.Status != OrderStatus.Cancelled)
                    .Sum(o => (decimal?)o.TotalAmount) ?? 0
            })
            .FirstOrDefaultAsync();

        if (tenant is null)
            return Result<TenantDetailDto>.NotFound("Tenant not found.");

        return Result<TenantDetailDto>.Success(new TenantDetailDto(
            tenant.Id,
            tenant.Name,
            tenant.Subdomain,
            tenant.StoreType.ToString(),
            tenant.PlanType.ToString(),
            tenant.IsActive,
            tenant.Settings?.LogoUrl,
            tenant.Settings?.StoreName,
            tenant.TotalUsers,
            tenant.TotalProducts,
            tenant.TotalOrders,
            tenant.TotalRevenue,
            tenant.CreatedAt
        ));
    }

    // ─── Update Tenant Status (activate / suspend) ────────────────────────────

    public async Task<Result<bool>> UpdateTenantStatusAsync(Guid id, UpdateTenantStatusRequest request)
    {
        var tenant = await _db.Tenants.FindAsync(id);

        if (tenant is null)
            return Result<bool>.NotFound("Tenant not found.");

        tenant.IsActive = request.IsActive;
        await _db.SaveChangesAsync();

        var action = request.IsActive ? AuditAction.Activated : AuditAction.Suspended;
        _auditLog.Log(action, nameof(Core.Entities.Tenant), tenant.Id,
            $"Tenant \"{tenant.Name}\" {(request.IsActive ? "activated" : "suspended")} by SuperAdmin.");

        return Result<bool>.Success(true);
    }

    // ─── Platform Analytics ───────────────────────────────────────────────────

    public async Task<Result<PlatformStatsDto>> GetPlatformStatsAsync()
    {
        var totalTenants  = await _db.Tenants.CountAsync();
        var activeTenants = await _db.Tenants.CountAsync(t => t.IsActive);
        var totalUsers    = await _db.Users.CountAsync();

        var thisMonthStart = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var newThisMonth   = await _db.Tenants.CountAsync(t => t.CreatedAt >= thisMonthStart);

        // Orders across all tenants — IgnoreQueryFilters not needed:
        // Order IS a TenantEntity, so filter is active. We need all tenants here.
        // Solution: query directly from the Orders DbSet with IgnoreQueryFilters.
        var orderStats = await _db.Orders
            .IgnoreQueryFilters()
            .Where(o => o.DeletedAt == null && o.Status != OrderStatus.Cancelled)
            .GroupBy(o => 1)
            .Select(g => new
            {
                TotalOrders  = g.Count(),
                TotalRevenue = g.Sum(o => (decimal?)o.TotalAmount) ?? 0
            })
            .FirstOrDefaultAsync();

        var planBreakdown = await _db.Tenants
            .GroupBy(t => t.PlanType)
            .Select(g => new PlanBreakdownDto(g.Key.ToString(), g.Count()))
            .ToListAsync();

        var storeTypeBreakdown = await _db.Tenants
            .GroupBy(t => t.StoreType)
            .Select(g => new StoreTypeBreakdownDto(g.Key.ToString(), g.Count()))
            .ToListAsync();

        return Result<PlatformStatsDto>.Success(new PlatformStatsDto(
            totalTenants,
            activeTenants,
            totalUsers,
            orderStats?.TotalOrders ?? 0,
            orderStats?.TotalRevenue ?? 0,
            newThisMonth,
            planBreakdown,
            storeTypeBreakdown
        ));
    }

    // ─── Platform Audit Log ───────────────────────────────────────────────────

    public async Task<Result<SuperAuditLogListDto>> GetAuditLogsAsync(int page = 1, int pageSize = 30)
    {
        // AuditLog is NOT a TenantEntity — no Global Query Filter on it
        var q = _db.AuditLogs
            .AsNoTracking()
            .OrderByDescending(a => a.CreatedAt);

        var totalCount = await q.CountAsync();

        var items = await q
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(a => new
            {
                a.Id,
                a.TenantId,
                a.UserEmail,
                a.Action,
                a.EntityType,
                a.EntityId,
                a.Details,
                a.IpAddress,
                a.CreatedAt,
                TenantName = _db.Tenants
                    .Where(t => t.Id == a.TenantId)
                    .Select(t => t.Name)
                    .FirstOrDefault()
            })
            .ToListAsync();

        var dtos = items.Select(a => new SuperAuditLogDto(
            a.Id,
            a.TenantName,
            a.UserEmail ?? "system",
            a.Action.ToString(),
            a.EntityType,
            a.EntityId,
            a.Details,
            a.IpAddress,
            a.CreatedAt
        )).ToList();

        var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

        return Result<SuperAuditLogListDto>.Success(new SuperAuditLogListDto(
            dtos,
            totalCount,
            page,
            pageSize,
            totalPages
        ));
    }
}
