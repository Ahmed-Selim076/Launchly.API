using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Launchly.API.Application.Booking;
using Launchly.API.Application.Booking.DTOs;
using Launchly.API.Common;

namespace Launchly.API.Controllers.Admin;

[ApiController]
[Route("api/v1/admin/booking")]
[Authorize(Policy = "TenantAdmin")]
public class BookingAdminController : ControllerBase
{
    private readonly BookingService _bookingService;

    public BookingAdminController(BookingService bookingService)
    {
        _bookingService = bookingService;
    }

    // ─── Services ─────────────────────────────────────────────────────────────

    [HttpGet("services")]
    public async Task<IActionResult> GetServices()
    {
        var result = await _bookingService.GetServicesAsync();
        return ToResponse(result);
    }

    [HttpGet("services/{id:guid}")]
    public async Task<IActionResult> GetService(Guid id)
    {
        var result = await _bookingService.GetServiceByIdAsync(id);
        return ToResponse(result);
    }

    [HttpPost("services")]
    public async Task<IActionResult> CreateService([FromBody] CreateServiceRequest request)
    {
        var result = await _bookingService.CreateServiceAsync(request);
        return ToResponse(result);
    }

    [HttpPut("services/{id:guid}")]
    public async Task<IActionResult> UpdateService(Guid id, [FromBody] UpdateServiceRequest request)
    {
        var result = await _bookingService.UpdateServiceAsync(id, request);
        return ToResponse(result);
    }

    [HttpDelete("services/{id:guid}")]
    public async Task<IActionResult> DeleteService(Guid id)
    {
        var result = await _bookingService.DeleteServiceAsync(id);
        return ToResponse(result);
    }

    // ─── Appointments ─────────────────────────────────────────────────────────

    [HttpGet("appointments")]
    public async Task<IActionResult> GetAppointments(
        [FromQuery] int page     = 1,
        [FromQuery] int pageSize = 20)
    {
        var result = await _bookingService.GetAppointmentsAsync(page, pageSize);
        return ToResponse(result);
    }

    [HttpPatch("appointments/{id:guid}/status")]
    public async Task<IActionResult> UpdateAppointmentStatus(
        Guid id,
        [FromBody] UpdateAppointmentStatusRequest request)
    {
        var result = await _bookingService.UpdateAppointmentStatusAsync(id, request);
        return ToResponse(result);
    }

    // ─── Helpers ──────────────────────────────────────────────────────────────

    private IActionResult ToResponse<T>(Result<T> result)
    {
        if (result.IsSuccess)
            return StatusCode(result.StatusCode, ApiResponse<T>.Ok(result.Value!));

        return StatusCode(result.StatusCode, ApiResponse<T>.Fail(
            result.Error ?? "An error occurred.",
            result.ValidationErrors
        ));
    }
}
