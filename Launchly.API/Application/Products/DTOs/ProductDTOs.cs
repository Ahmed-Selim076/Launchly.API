namespace Launchly.API.Application.Products.DTOs;

// ─── Requests ─────────────────────────────────────────────────────────────────

public record CreateProductRequest(
    string Name,
    string? Description,
    decimal Price,
    int Stock,
    Guid? CategoryId,
    string? ImageUrl,
    bool IsActive
);

public record UpdateProductRequest(
    string Name,
    string? Description,
    decimal Price,
    int Stock,
    Guid? CategoryId,
    string? ImageUrl,
    bool IsActive
);

public record ProductsQuery(
    int Page = 1,
    int PageSize = 20,
    string? Search = null,
    Guid? CategoryId = null,
    bool? IsActive = null
);

// ─── Responses ────────────────────────────────────────────────────────────────

public record ProductDto(
    Guid Id,
    string Name,
    string Slug,
    string? Description,
    decimal Price,
    int Stock,
    string? ImageUrl,
    bool IsActive,
    Guid? CategoryId,
    string? CategoryName,
    DateTime CreatedAt,
    DateTime UpdatedAt
);
