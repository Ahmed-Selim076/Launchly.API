namespace Launchly.API.Application.AuditLog.DTOs;

public record AuditLogDto(
    Guid Id,
    string UserEmail,
    string Action,
    string? EntityType,
    Guid? EntityId,
    string? Details,
    string? IpAddress,
    DateTime CreatedAt
);

public record AuditLogListDto(
    IReadOnlyList<AuditLogDto> Items,
    int TotalCount,
    int Page,
    int PageSize,
    int TotalPages
);
