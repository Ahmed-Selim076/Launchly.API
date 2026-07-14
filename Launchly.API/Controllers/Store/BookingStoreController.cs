using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Launchly.API.Application.Booking;
using Launchly.API.Application.Booking.DTOs;
using Launchly.API.Common;
using Launchly.API.Core.Interfaces;

namespace Launchly.API.Controllers.Store;

[ApiController]
[Route("api/v1/store/booking")]
public class BookingStoreController : ControllerBase
{
    private readonly BookingService _bookingService;
    private readonly ICurrentUser   _currentUser;

    public BookingStoreController(BookingService bookingService, ICurrentUser currentUser)
    {
        _bookingService = bookingService;
        _currentUser    = currentUser;
    }

    // ─── Public: Services ─────────────────────────────────────────────────────

    /// <summary>Returns all active services for this store.</summary>
    [HttpGet("services")]
    public async Task<IActionResult> GetServices()
    {
        var result = await _bookingService.GetServicesAsync();
        return ToResponse(result);
    }

    /// <summary>Returns a single service by ID.</summary>
    [HttpGet("services/{id:guid}")]
    public async Task<IActionResult> GetService(Guid id)
    {
        var result = await _bookingService.GetServiceByIdAsync(id);
        return ToResponse(result);
    }

    // ─── Public: Availability ─────────────────────────────────────────────────

    /// <summary>
    /// Returns available booking slots for a given service on a given date.
    /// <paramref name="date"/> should be passed as a date-only value (e.g. 2026-07-15).
    /// </summary>
    [HttpGet("services/{serviceId:guid}/availability")]
    public async Task<IActionResult> GetAvailability(
        Guid serviceId,
        [FromQuery] DateTime date)
    {
        var result = await _bookingService.GetAvailableSlotsAsync(serviceId, date);
        return ToResponse(result);
    }

    // ─── Customer: Appointments ───────────────────────────────────────────────

    /// <summary>Books an appointment. Requires customer authentication.</summary>
    [HttpPost("appointments")]
    [Authorize(Policy = "Customer")]
    public async Task<IActionResult> BookAppointment([FromBody] BookAppointmentRequest request)
    {
        var result = await _bookingService.BookAppointmentAsync(request, _currentUser.Id);
        return ToResponse(result);
    }

    /// <summary>Returns all appointments for the authenticated customer.</summary>
    [HttpGet("appointments/my")]
    [Authorize(Policy = "Customer")]
    public async Task<IActionResult> GetMyAppointments()
    {
        var result = await _bookingService.GetCustomerAppointmentsAsync(_currentUser.Id);
        return ToResponse(result);
    }

    /// <summary>Allows a customer to cancel their own appointment.</summary>
    [HttpPatch("appointments/{id:guid}/cancel")]
    [Authorize(Policy = "Customer")]
    public async Task<IActionResult> CancelAppointment(Guid id)
    {
        var result = await _bookingService.CancelAppointmentAsync(id, _currentUser.Id);
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
