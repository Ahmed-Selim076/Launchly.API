namespace Launchly.API.Application.Orders.DTOs;

// ─── Requests ─────────────────────────────────────────────────────────────────

public record CreateOrderRequest(
    Guid CustomerId,
    int? OrderType,
    string? Notes,
    List<CreateOrderItemRequest> Items
);

public record CreateOrderItemRequest(
    Guid ProductId,
    int Quantity
);

public record UpdateOrderStatusRequest(
    int Status
);

public record OrdersQuery(
    int Page = 1,
    int PageSize = 20,
    int? Status = null,
    Guid? CustomerId = null
);

// ─── Responses ────────────────────────────────────────────────────────────────

public record OrderDto(
    Guid Id,
    Guid CustomerId,
    string CustomerName,
    string Status,
    string? OrderType,
    decimal TotalAmount,
    string? Notes,
    List<OrderItemDto> Items,
    DateTime CreatedAt,
    DateTime UpdatedAt
);

public record OrderItemDto(
    Guid Id,
    Guid? ProductId,
    string Name,
    decimal UnitPrice,
    int Quantity,
    decimal LineTotal
);
