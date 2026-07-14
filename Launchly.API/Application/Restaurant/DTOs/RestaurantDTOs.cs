namespace Launchly.API.Application.Restaurant.DTOs;

// ─── Menu Category ────────────────────────────────────────────────────────────

public record CreateMenuCategoryRequest(
    string Name,
    int SortOrder
);

public record UpdateMenuCategoryRequest(
    string Name,
    int SortOrder
);

public record MenuCategoryDto(
    Guid Id,
    string Name,
    int SortOrder,
    int ItemCount
);

// ─── Menu Item ────────────────────────────────────────────────────────────────

public record CreateMenuItemRequest(
    string Name,
    string? Description,
    decimal Price,
    Guid? CategoryId,
    string? ImageUrl,
    bool IsActive
);

public record UpdateMenuItemRequest(
    string Name,
    string? Description,
    decimal Price,
    Guid? CategoryId,
    string? ImageUrl,
    bool IsActive
);

public record MenuItemDto(
    Guid Id,
    string Name,
    string? Description,
    decimal Price,
    Guid? CategoryId,
    string? CategoryName,
    string? ImageUrl,
    bool IsActive,
    DateTime CreatedAt
);

// ─── Food Order ───────────────────────────────────────────────────────────────

public record CreateFoodOrderRequest(
    Guid CustomerId,
    string? Notes,
    List<CreateFoodOrderItemRequest> Items
);

public record CreateFoodOrderItemRequest(
    Guid MenuItemId,
    int Quantity
);

public record FoodOrderItemDto(
    Guid Id,
    Guid? MenuItemId,
    string Name,
    decimal UnitPrice,
    int Quantity,
    decimal LineTotal
);

public record FoodOrderDto(
    Guid Id,
    string CustomerName,
    string CustomerEmail,
    string Status,
    string? Notes,
    decimal TotalAmount,
    List<FoodOrderItemDto> Items,
    DateTime CreatedAt
);

public record FoodOrderQueryRequest(
    int Page = 1,
    int PageSize = 20,
    int? Status = null
);

public record UpdateFoodOrderStatusRequest(int Status);

// ─── Public Menu (storefront-facing, grouped) ─────────────────────────────────
// Used by the public GET /api/v1/store/menu endpoint only — distinct from
// MenuItemDto/MenuCategoryDto above (which are admin-facing and flat). The
// storefront wants categories with their items already nested, in one call,
// rather than fetching categories and items separately and joining them
// client-side.

public record PublicMenuItemDto(
    Guid Id,
    string Name,
    string? Description,
    decimal Price,
    string? ImageUrl
);

public record PublicMenuCategoryDto(
    Guid Id,
    string Name,
    int SortOrder,
    List<PublicMenuItemDto> Items
);
