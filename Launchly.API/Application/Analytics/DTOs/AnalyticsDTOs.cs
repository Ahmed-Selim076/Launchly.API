namespace Launchly.API.Application.Analytics.DTOs;

// ─── Dashboard (already exists — keeping for reference) ───────────────────────

// TotalCatalogItems / TopCatalogItems are intentionally storeType-neutral
// names (not "TotalProducts"/"TopProducts"): the same Dashboard endpoint
// serves Ecommerce (Products), Booking (Services), and Restaurant
// (MenuItems) tenants — see DashboardService for the per-StoreType
// branching that populates these fields from the right table.
//
// This is separate from TopProductDto below, which backs the dedicated
// /analytics/top-products endpoint — that one is intentionally
// Ecommerce-only (AnalyticsService.GetTopProductsAsync), not a duplicate
// to consolidate.
public record DashboardDto(
    int TotalOrders,
    decimal TotalRevenue,
    int TotalCatalogItems,
    int TotalCustomers,
    int PendingOrders,
    IReadOnlyList<TopCatalogItemDto> TopCatalogItems
);

public record TopCatalogItemDto(
    Guid ItemId,
    string ItemName,
    int TotalSold,
    decimal TotalRevenue
);

// ─── Sales Analytics ──────────────────────────────────────────────────────────

public record SalesChartDto(
    IReadOnlyList<SalesDataPointDto> Points,
    decimal TotalRevenue,
    int TotalOrders,
    decimal AverageOrderValue
);

public record SalesDataPointDto(
    string Label,       // "2026-06-01" for daily, "2026-W23" for weekly
    decimal Revenue,
    int OrderCount
);

// ─── Visitor Analytics ────────────────────────────────────────────────────────

public record VisitorStatsDto(
    int TodayUniqueVisitors,
    int WeeklyUniqueVisitors,
    int MonthlyUniqueVisitors,
    IReadOnlyList<VisitorDataPointDto> DailyPoints
);

public record VisitorDataPointDto(
    string Date,        // "2026-06-01"
    int UniqueVisitors
);

// ─── Top Products (Ecommerce-only — see note above DashboardDto) ─────────────

public record TopProductDto(
    Guid ProductId,
    string ProductName,
    int TotalSold,
    decimal TotalRevenue
);

public record TopProductsDto(
    IReadOnlyList<TopProductDto> Products,
    string Period       // "7d", "30d", "90d"
);
