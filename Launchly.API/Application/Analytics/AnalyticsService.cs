using Microsoft.EntityFrameworkCore;
using Launchly.API.Application.Analytics.DTOs;
using Launchly.API.Common;
using Launchly.API.Core.Enums;
using Launchly.API.Core.Interfaces;
using Launchly.API.Infrastructure.Data;

namespace Launchly.API.Application.Analytics;

public class AnalyticsService
{
    private readonly AppDbContext _db;
    private readonly ITenantContext _tenantContext;

    public AnalyticsService(AppDbContext db, ITenantContext tenantContext)
    {
        _db = db;
        _tenantContext = tenantContext;
    }

    // ─── Sales Chart ──────────────────────────────────────────────────────────
    // period: "7d" | "30d" | "90d"

    public async Task<Result<SalesChartDto>> GetSalesAsync(string period = "30d")
    {
        if (_tenantContext.TenantId is null)
            return Result<SalesChartDto>.Failure("Store context is required.");

        var tenantId = _tenantContext.TenantId.Value;

        var tenant = await _db.Tenants
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == tenantId);

        if (tenant is null)
            return Result<SalesChartDto>.Failure("Store context is required.");

        var (startDate, groupByWeek) = period switch
        {
            "7d"  => (DateTime.UtcNow.AddDays(-7),  false),
            "90d" => (DateTime.UtcNow.AddDays(-90), true),
            _     => (DateTime.UtcNow.AddDays(-30), false)   // default: 30d
        };

        // "Revenue events" mean a different table per StoreType — same
        // reasoning as DashboardService/OnboardingService. Booking has no
        // Orders at all (completed Appointments stand in for them, with
        // Service.Price as the revenue amount); Ecommerce and Restaurant
        // both read from the shared Orders table (Restaurant orders are
        // just Orders whose items happen to carry a MenuItemId, not a
        // separate entity — see RestaurantService.GetOrdersAsync).
        var events = tenant.StoreType == StoreType.Booking
            ? await _db.Appointments
                .AsNoTracking()
                .Where(a => a.Status == AppointmentStatus.Completed &&
                            a.StartTime >= startDate)
                .Include(a => a.Service)
                .Select(a => new { CreatedAt = a.StartTime, Amount = a.Service.Price })
                .ToListAsync()
            : await _db.Orders
                .AsNoTracking()
                .Where(o => o.Status != OrderStatus.Cancelled &&
                            o.CreatedAt >= startDate)
                .Select(o => new { o.CreatedAt, Amount = o.TotalAmount })
                .ToListAsync();

        // Group by day or week
        var points = groupByWeek
            ? events
                .GroupBy(e => $"{e.CreatedAt:yyyy}-W{System.Globalization.ISOWeek.GetWeekOfYear(e.CreatedAt):D2}")
                .OrderBy(g => g.Key)
                .Select(g => new SalesDataPointDto(g.Key, g.Sum(e => e.Amount), g.Count()))
                .ToList()
            : events
                .GroupBy(e => e.CreatedAt.ToString("yyyy-MM-dd"))
                .OrderBy(g => g.Key)
                .Select(g => new SalesDataPointDto(g.Key, g.Sum(e => e.Amount), g.Count()))
                .ToList();

        var totalRevenue = events.Sum(e => e.Amount);
        var totalOrders  = events.Count;
        var avgOrderValue = totalOrders > 0 ? Math.Round(totalRevenue / totalOrders, 2) : 0;

        return Result<SalesChartDto>.Success(new SalesChartDto(
            points,
            totalRevenue,
            totalOrders,
            avgOrderValue
        ));
    }

    // ─── Visitor Stats ────────────────────────────────────────────────────────

    public async Task<Result<VisitorStatsDto>> GetVisitorsAsync()
    {
        if (_tenantContext.TenantId is null)
            return Result<VisitorStatsDto>.Failure("Store context is required.");

        var now     = DateTime.UtcNow;
        var today   = now.Date;
        var week    = now.AddDays(-7);
        var month   = now.AddDays(-30);

        // Pull last 30 days of visitor logs for this tenant
        // (Global Query Filter does NOT apply to VisitorLogs — filter manually)
        var logs = await _db.VisitorLogs
            .AsNoTracking()
            .Where(v => v.TenantId == _tenantContext.TenantId &&
                        v.VisitedAt >= month)
            .Select(v => new { v.IpHash, v.VisitedAt })
            .ToListAsync();

        var todayUnique   = logs.Where(v => v.VisitedAt.Date == today).Select(v => v.IpHash).Distinct().Count();
        var weeklyUnique  = logs.Where(v => v.VisitedAt >= week).Select(v => v.IpHash).Distinct().Count();
        var monthlyUnique = logs.Select(v => v.IpHash).Distinct().Count();

        // Daily breakdown for chart (last 30 days)
        var dailyPoints = logs
            .GroupBy(v => v.VisitedAt.ToString("yyyy-MM-dd"))
            .OrderBy(g => g.Key)
            .Select(g => new VisitorDataPointDto(g.Key, g.Select(v => v.IpHash).Distinct().Count()))
            .ToList();

        return Result<VisitorStatsDto>.Success(new VisitorStatsDto(
            todayUnique,
            weeklyUnique,
            monthlyUnique,
            dailyPoints
        ));
    }

    // ─── Top Products ─────────────────────────────────────────────────────────

    public async Task<Result<TopProductsDto>> GetTopProductsAsync(string period = "30d")
    {
        if (_tenantContext.TenantId is null)
            return Result<TopProductsDto>.Failure("Store context is required.");

        var startDate = period switch
        {
            "7d"  => DateTime.UtcNow.AddDays(-7),
            "90d" => DateTime.UtcNow.AddDays(-90),
            _     => DateTime.UtcNow.AddDays(-30)
        };

        // Safe: querying through Orders (tenant-filtered) then SelectMany into Items
        var items = await _db.Orders
            .AsNoTracking()
            .Where(o => o.Status != OrderStatus.Cancelled &&
                        o.CreatedAt >= startDate)
            .SelectMany(o => o.Items)
            .Where(i => i.ProductId.HasValue)
            .Select(i => new { i.ProductId, i.Name, i.UnitPrice, i.Quantity })
            .ToListAsync();

        var topProducts = items
            .GroupBy(i => new { i.ProductId, i.Name })
            .Select(g => new TopProductDto(
                g.Key.ProductId!.Value,
                g.Key.Name,
                g.Sum(i => i.Quantity),
                g.Sum(i => i.UnitPrice * i.Quantity)
            ))
            .OrderByDescending(p => p.TotalSold)
            .Take(10)
            .ToList();

        return Result<TopProductsDto>.Success(new TopProductsDto(topProducts, period));
    }
}
