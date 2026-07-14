namespace Launchly.API.Application.Booking.DTOs;

// ─── Service ──────────────────────────────────────────────────────────────────

public record CreateServiceRequest(
    string Name,
    string? Description,
    int DurationMins,
    decimal Price,
    string? ImageUrl
);

public record UpdateServiceRequest(
    string Name,
    string? Description,
    int DurationMins,
    decimal Price,
    bool IsActive,
    string? ImageUrl
);

public record ServiceDto(
    Guid Id,
    string Name,
    string? Description,
    int DurationMins,
    decimal Price,
    bool IsActive,
    DateTime CreatedAt,
    string? ImageUrl
);

// ─── Availability ─────────────────────────────────────────────────────────────

public record AvailableSlotDto(
    DateTime StartTime,
    DateTime EndTime
);

// ─── Appointment ─────────────────────────────────────────────────────────────

public record BookAppointmentRequest(
    Guid ServiceId,
    DateTime StartTime,
    string? Notes
);

public record AppointmentDto(
    Guid Id,
    Guid ServiceId,
    string ServiceName,
    string CustomerName,
    string CustomerEmail,
    DateTime StartTime,
    DateTime EndTime,
    string Status,
    string? Notes,
    DateTime CreatedAt
);

public record AppointmentListDto(
    IReadOnlyList<AppointmentDto> Items,
    int TotalCount,
    int Page,
    int PageSize,
    int TotalPages
);

public record UpdateAppointmentStatusRequest(
    string Status
);

// ─── Customer: Own Appointment ────────────────────────────────────────────────

public record CustomerAppointmentDto(
    Guid Id,
    string ServiceName,
    int ServiceDurationMins,
    decimal ServicePrice,
    DateTime StartTime,
    DateTime EndTime,
    string Status,
    string? Notes,
    DateTime CreatedAt
);
