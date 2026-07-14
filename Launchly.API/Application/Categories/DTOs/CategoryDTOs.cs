namespace Launchly.API.Application.Categories.DTOs;

public record CreateCategoryRequest(
    string Name,
    int SortOrder
);

public record UpdateCategoryRequest(
    string Name,
    int SortOrder
);

public record CategoryDto(
    Guid Id,
    string Name,
    int SortOrder,
    int ProductCount
);