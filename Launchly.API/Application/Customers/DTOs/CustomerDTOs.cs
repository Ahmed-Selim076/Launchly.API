namespace Launchly.API.Application.Customers.DTOs;

public record CustomerDto(
    Guid Id,
    string FirstName,
    string LastName,
    string Email,
    bool IsActive,
    DateTime CreatedAt
);

public record CustomerListDto(
    IReadOnlyList<CustomerDto> Items,
    int TotalCount,
    int Page,
    int PageSize,
    int TotalPages
);
