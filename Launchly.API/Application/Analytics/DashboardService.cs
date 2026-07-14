using Microsoft.EntityFrameworkCore;
using Launchly.API.Application.Analytics.DTOs;
using Launchly.API.Common;
using Launchly.API.Core.Enums;
using Launchly.API.Core.Interfaces;
using Launchly.API.Infrastructure.Data;

namespace Launchly.API.Application.Analytics;

public class DashboardService
{
    private readonly AppDbContext _db;
    private readonly ITenantContext _tenantContext;

    public DashboardService(AppDbContext db, ITenantContext tenantContext)
    {
        _db = db;
        _tenantContext = tenantContext;
    }

    public async Task<Result<DashboardDto>> GetDashboardAsync()
    {
        if (_tenantContext.TenantId is null)
            return Result<DashboardDto>.Failure("Store context is required.");

        var tenantId = _tenantContext.TenantId.Value;

        var tenant = await _db.Tenants
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == tenantId);

        if (tenant is null)
            return Result<DashboardDto>.Failure("Store context is required.");

        var totalCustomers = await _db.Users
            .CountAsync(u => u.TenantId == tenantId && u.Role == UserRole.Customer);

        // "Orders", "catalog item count", and "top sellers" all mean a
        // different table per StoreType — Booking has no Orders at all
        // (it has Appointments instead), and Restaurant orders are rows
        // in the shared Orders table identified by having a MenuItemId
        // line rather than a separate entity. See OnboardingService for
        // the same branching reasoning applied there.
        return tenant.StoreType switch
        {
            StoreType.Booking    => await GetBookingDashboardAsync(totalCustomers),
            StoreType.Restaurant => await GetRestaurantDashboardAsync(totalCustomers),
            _                    => await GetEcommerceDashboardAsync(totalCustomers),
        };
    }

    // ─── Ecommerce ────────────────────────────────────────────────────────────

    private async Task<Result<DashboardDto>> GetEcommerceDashboardAsync(int totalCustomers)
    {
        var totalOrders = await _db.Orders.CountAsync();

        var totalRevenue = await _db.Orders
            .Where(o => o.Status != OrderStatus.Cancelled)
            .SumAsync(o => (decimal?)o.TotalAmount) ?? 0;

        var totalCatalogItems = await _db.Products.CountAsync(p => p.IsActive);

        var pendingOrders = await _db.Orders
            .CountAsync(o => o.Status == OrderStatus.Pending);

        // Query through Orders (tenant-filtered) rather than OrderItems directly —
        // OrderItem has no TenantId/query filter of its own, so querying it
        // directly would leak line items across tenants.
        //
        // The GroupBy-after-SelectMany shape below isn't translatable by EF Core's
        // SQL provider, so we materialize the flat (small, tenant-scoped) item list
        // first and do the aggregation in memory.
        var tenantOrderItems = await _db.Orders
            .Where(o => o.Status != OrderStatus.Cancelled)
            .SelectMany(o => o.Items)
            .Where(i => i.ProductId.HasValue)
            .Select(i => new { i.ProductId, i.Name, i.UnitPrice, i.Quantity })
            .ToListAsync();

        var topItems = tenantOrderItems
            .GroupBy(i => new { i.ProductId, i.Name })
            .Select(g => new TopCatalogItemDto(
                g.Key.ProductId!.Value,
                g.Key.Name,
                g.Sum(i => i.Quantity),
                g.Sum(i => i.UnitPrice * i.Quantity)
            ))
            .OrderByDescending(p => p.TotalSold)
            .Take(5)
            .ToList();

        return Result<DashboardDto>.Success(new DashboardDto(
            totalOrders, totalRevenue, totalCatalogItems, totalCustomers, pendingOrders, topItems
        ));
    }

    // ─── Booking ──────────────────────────────────────────────────────────────

    private async Task<Result<DashboardDto>> GetBookingDashboardAsync(int totalCustomers)
    {
        // Booking has no "Orders" — Appointments stand in for them: each
        // completed appointment is the booking equivalent of a fulfilled
        // order, and Service.Price stands in for revenue.
        var totalAppointments = await _db.Appointments.CountAsync();

        var totalRevenue = await _db.Appointments
            .Where(a => a.Status == AppointmentStatus.Completed)
            .Include(a => a.Service)
            .SumAsync(a => (decimal?)a.Service.Price) ?? 0;

        var totalCatalogItems = await _db.Services.CountAsync(s => s.IsActive);

        var pendingAppointments = await _db.Appointments
            .CountAsync(a => a.Status == AppointmentStatus.Pending);

        var tenantAppointments = await _db.Appointments
            .Where(a => a.Status == AppointmentStatus.Completed)
            .Include(a => a.Service)
            .Select(a => new { a.ServiceId, a.Service.Name, a.Service.Price })
            .ToListAsync();

        var topItems = tenantAppointments
            .GroupBy(a => new { a.ServiceId, a.Name })
            .Select(g => new TopCatalogItemDto(
                g.Key.ServiceId,
                g.Key.Name,
                g.Count(),
                g.Sum(a => a.Price)
            ))
            .OrderByDescending(s => s.TotalSold)
            .Take(5)
            .ToList();

        return Result<DashboardDto>.Success(new DashboardDto(
            totalAppointments, totalRevenue, totalCatalogItems, totalCustomers, pendingAppointments, topItems
        ));
    }

    // ─── Restaurant ───────────────────────────────────────────────────────────

    private async Task<Result<DashboardDto>> GetRestaurantDashboardAsync(int totalCustomers)
    {
        // Restaurant orders are rows in the shared Orders table that have
        // at least one line item with a MenuItemId — see
        // RestaurantService.GetOrdersAsync for the same filter used there.
        var restaurantOrders = _db.Orders
            .Where(o => o.Items.Any(i => i.MenuItemId != null));

        var totalOrders = await restaurantOrders.CountAsync();

        var totalRevenue = await restaurantOrders
            .Where(o => o.Status != OrderStatus.Cancelled)
            .SumAsync(o => (decimal?)o.TotalAmount) ?? 0;

        var totalCatalogItems = await _db.MenuItems.CountAsync(m => m.IsActive);

        var pendingOrders = await restaurantOrders
            .CountAsync(o => o.Status == OrderStatus.Pending);

        var tenantOrderItems = await restaurantOrders
            .Where(o => o.Status != OrderStatus.Cancelled)
            .SelectMany(o => o.Items)
            .Where(i => i.MenuItemId.HasValue)
            .Select(i => new { i.MenuItemId, i.Name, i.UnitPrice, i.Quantity })
            .ToListAsync();

        var topItems = tenantOrderItems
            .GroupBy(i => new { i.MenuItemId, i.Name })
            .Select(g => new TopCatalogItemDto(
                g.Key.MenuItemId!.Value,
                g.Key.Name,
                g.Sum(i => i.Quantity),
                g.Sum(i => i.UnitPrice * i.Quantity)
            ))
            .OrderByDescending(p => p.TotalSold)
            .Take(5)
            .ToList();

        return Result<DashboardDto>.Success(new DashboardDto(
            totalOrders, totalRevenue, totalCatalogItems, totalCustomers, pendingOrders, topItems
        ));
    }
}