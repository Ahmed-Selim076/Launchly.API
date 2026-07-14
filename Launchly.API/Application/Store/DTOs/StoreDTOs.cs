namespace Launchly.API.Application.Store.DTOs;

// ─── Store Settings (Public) ──────────────────────────────────────────────────

public record PublicStoreSettingsDto(
    string StoreName,
    string? LogoUrl,
    string PrimaryColor,
    string SecondaryColor,
    string? HeroText,
    string? AboutText,
    string? GoogleAnalyticsId,
    string StoreType,
    int TemplateId,
    string? ContactPhone,
    string? WhatsappNumber,
    string? ContactEmail,
    string? ContactAddress,
    string? FacebookUrl,
    string? InstagramUrl
);

// ─── Contact (Public) ──────────────────────────────────────────────────────────

public record ContactMessageRequest(
    string Name,
    string Email,
    string? Phone,
    string? Subject,
    string Message
);

// ─── Products (Public) ────────────────────────────────────────────────────────

public record PublicProductDto(
    Guid Id,
    string Name,
    string Slug,
    string? Description,
    decimal Price,
    int Stock,
    string? ImageUrl,
    string? CategoryName,
    decimal? OriginalPrice,
    string? Badge,
    double AverageRating,
    int ReviewCount,
    // Null when the request is unauthenticated (no way to know) — the
    // storefront treats null the same as false but never overwrites a
    // logged-in customer's real wishlist state with a false negative.
    bool? IsWishlisted
);

public record PublicProductListDto(
    IReadOnlyList<PublicProductDto> Items,
    int TotalCount,
    int Page,
    int PageSize,
    int TotalPages
);

// ─── Wishlist (Customer) ───────────────────────────────────────────────────────

public record WishlistItemDto(
    Guid ProductId,
    string Name,
    string Slug,
    decimal Price,
    decimal? OriginalPrice,
    string? ImageUrl,
    int Stock,
    DateTime AddedAt
);

// ─── Reviews (Public + Customer) ───────────────────────────────────────────────

public record ReviewDto(
    Guid Id,
    string CustomerName,
    int Rating,
    string? Comment,
    DateTime CreatedAt
);

public record ReviewSummaryDto(
    double AverageRating,
    int ReviewCount,
    IReadOnlyList<ReviewDto> Items
);

public record CreateReviewRequest(
    int Rating,
    string? Comment
);

// ─── Categories (Public) ──────────────────────────────────────────────────────
// Used by the Ecommerce storefront's product grid to render the category
// filter sidebar/dropdown. Deliberately thin (id/name/sortOrder only) —
// the storefront never needs product counts per category here.

public record PublicCategoryDto(
    Guid Id,
    string Name,
    int SortOrder,
    int ProductCount
);

// ─── Customer Registration ────────────────────────────────────────────────────

public record RegisterCustomerRequest(
    string FirstName,
    string LastName,
    string Email,
    string Password
);

// ─── Place Order ──────────────────────────────────────────────────────────────

// PlaceOrderRequest serves both Ecommerce and Restaurant storefronts.
// OrderType is required for Restaurant (Delivery/Pickup) and ignored for
// Ecommerce (left null) — same nullable OrderType already on the Order
// entity, just exposed at the public API boundary now.
public record PlaceOrderRequest(
    IReadOnlyList<OrderLineRequest> Items,
    string? Notes,
    int? OrderType
);

// Exactly one of ProductId/MenuItemId must be set per line — which one
// depends on which storefront is calling. Mirrors OrderItem's own shape
// (see Core/Entities/Entities.cs), which already supports both as nullable
// for exactly this reason (Restaurant orders are rows in the same Orders
// table as Ecommerce, distinguished by which id is populated).
public record OrderLineRequest(
    Guid? ProductId,
    Guid? MenuItemId,
    int Quantity
);

public record PlacedOrderDto(
    Guid OrderId,
    decimal TotalAmount,
    string Status,
    DateTime CreatedAt
);

// ─── Order Detail (Customer) ──────────────────────────────────────────────────

public record CustomerOrderDto(
    Guid Id,
    string Status,
    decimal TotalAmount,
    string? Notes,
    DateTime CreatedAt,
    IReadOnlyList<CustomerOrderItemDto> Items
);

public record CustomerOrderItemDto(
    string Name,
    decimal UnitPrice,
    int Quantity,
    decimal LineTotal
);

// ─── Visitor ──────────────────────────────────────────────────────────────────

public record LogVisitRequest(
    string? Page
);
